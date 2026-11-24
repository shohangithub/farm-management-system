using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Registry;
using Farm360.Domain.Reporting;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Reporting.Services;

public sealed record ReportRequest(
    string Key,
    IReadOnlyDictionary<string, string?> Parameters,
    ReportLanguage Language = ReportLanguage.En,
    bool BengaliNumerals = false);

public sealed record RenderedReport(
    byte[] Content,
    string ContentType,
    string FileName,
    Guid RunId,
    int RowCount);

public interface IReportExecutionService
{
    IReadOnlyList<ReportDescriptor> GetCatalog();

    /// <summary>The catalog filtered to reports the current user is permitted to run.</summary>
    Task<IReadOnlyList<ReportDescriptor>> GetCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>True when the current user holds <paramref name="permission"/> (or it is blank).</summary>
    Task<bool> HasPermissionAsync(string? permission, CancellationToken cancellationToken = default);

    ReportDescriptor GetMetadata(string key);

    Task<ReportDataSet> RunAsync(ReportRequest request, CancellationToken cancellationToken = default);

    Task<RenderedReport> ExportAsync(ReportRequest request, ReportExportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs a report definition under the caller's tenant and permission scope, records the run for
/// audit, and hands the dataset to whichever renderer the caller asked for.
/// </summary>
/// <remarks>
/// Authorization and tenancy are enforced here, once, rather than inside each definition.
/// A report is the easiest place in an application to leak another tenant's data, and thirty
/// definitions each remembering to check for themselves is thirty chances to forget.
/// </remarks>
public sealed class ReportExecutionService : IReportExecutionService
{
    /// <summary>
    /// Refuse rather than exhaust memory. A request this large is a missing filter, not a
    /// legitimate report — and failing loudly at noon beats an OutOfMemoryException at 3 a.m.
    /// </summary>
    private const int MaxRows = 200_000;

    private static readonly JsonSerializerOptions ParameterJsonOptions = new() { WriteIndented = false };

    private readonly IReportRegistry _registry;
    private readonly ISender _sender;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissionService;
    private readonly IDateTimeService _dateTime;
    private readonly IReportRunRepository _runRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnumerable<IReportRenderer> _renderers;
    private readonly IReportEnvironment _environment;
    private readonly ILogger<ReportExecutionService> _logger;

    public ReportExecutionService(
        IReportRegistry registry,
        ISender sender,
        ITenantService tenantService,
        ICurrentUserService currentUser,
        IPermissionService permissionService,
        IDateTimeService dateTime,
        IReportRunRepository runRepository,
        IUnitOfWork unitOfWork,
        IEnumerable<IReportRenderer> renderers,
        IReportEnvironment environment,
        ILogger<ReportExecutionService> logger)
    {
        _registry = registry;
        _sender = sender;
        _tenantService = tenantService;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _dateTime = dateTime;
        _runRepository = runRepository;
        _unitOfWork = unitOfWork;
        _renderers = renderers;
        _environment = environment;
        _logger = logger;
    }

    public IReadOnlyList<ReportDescriptor> GetCatalog() => _registry.All;

    public async Task<IReadOnlyList<ReportDescriptor>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsSystemUser)
        {
            return _registry.All;
        }

        if (_currentUser.UserId is not { } userId)
        {
            return _registry.All.Where(d => d.Permission is not { Length: > 0 }).ToList();
        }

        // One permission load for the whole catalog, not one check per report.
        var granted = new HashSet<string>(
            await _permissionService.GetPermissionsAsync(userId, _tenantService.TenantId, cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);

        return _registry.All
            .Where(d => d.Permission is not { Length: > 0 } permission || granted.Contains(permission))
            .ToList();
    }

    public Task<bool> HasPermissionAsync(string? permission, CancellationToken cancellationToken = default) =>
        CanRunAsync(permission, cancellationToken);

    public ReportDescriptor GetMetadata(string key) => _registry.Describe(key);

    public Task<ReportDataSet> RunAsync(ReportRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(request, ReportOutputFormat.Screen, cancellationToken);

    public async Task<RenderedReport> ExportAsync(
        ReportRequest request,
        ReportExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var renderer = _renderers.FirstOrDefault(r => r.Format == format)
            ?? throw new InvalidOperationException($"No renderer is registered for format '{format}'.");

        var outputFormat = format switch
        {
            ReportExportFormat.Pdf => ReportOutputFormat.Pdf,
            ReportExportFormat.Xlsx => ReportOutputFormat.Xlsx,
            _ => ReportOutputFormat.Csv,
        };

        var (dataSet, run, stopwatch) = await ExecuteCoreAsync(request, outputFormat, cancellationToken)
            .ConfigureAwait(false);

        var content = await renderer.RenderAsync(dataSet, cancellationToken).ConfigureAwait(false);

        run.Complete(
            dataSet.TotalRowCount,
            (int)stopwatch.ElapsedMilliseconds,
            Convert.ToHexString(SHA256.HashData(content)),
            content.LongLength);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RenderedReport(
            content,
            renderer.ContentType,
            BuildFileName(dataSet, renderer.FileExtension),
            run.Id,
            dataSet.TotalRowCount);
    }

    private async Task<ReportDataSet> ExecuteAsync(
        ReportRequest request,
        ReportOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        var (dataSet, run, stopwatch) = await ExecuteCoreAsync(request, outputFormat, cancellationToken)
            .ConfigureAwait(false);

        run.Complete(dataSet.TotalRowCount, (int)stopwatch.ElapsedMilliseconds);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return dataSet;
    }

    private async Task<(ReportDataSet DataSet, ReportRun Run, Stopwatch Stopwatch)> ExecuteCoreAsync(
        ReportRequest request,
        ReportOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = _registry.Get(request.Key);

        await EnsurePermittedAsync(definition, cancellationToken).ConfigureAwait(false);

        var parameters = await BindAndValidateAsync(definition, request.Parameters, request.Language, cancellationToken)
            .ConfigureAwait(false);

        // The farm decides the clock: "today", the date presets and the printed timestamp are all
        // in the farm's zone, and the fiscal-year presets use the farm's financial-year start.
        var farmId = FindFarmId(definition, parameters);
        var locale = await _environment.GetLocaleAsync(farmId, cancellationToken).ConfigureAwait(false);
        var nowUtc = _dateTime.UtcNow;
        var userName = await _environment.GetUserDisplayNameAsync(cancellationToken).ConfigureAwait(false);

        var context = new ReportContext(
            parameters,
            _sender,
            _tenantService.TenantId,
            _currentUser.UserId,
            userName,
            _currentUser.AssignedFarmIds,
            ReportTimeZone.Today(nowUtc, locale.TimeZone),
            request.Language,
            request.BengaliNumerals,
            locale.TimeZone,
            locale.FiscalYearStartMonth,
            MaxRows);

        var run = ReportRun.Start(
            _tenantService.TenantId,
            definition.Key,
            definition.Title.For(request.Language),
            JsonSerializer.Serialize(parameters, ParameterJsonOptions),
            outputFormat,
            _currentUser.UserId,
            context.UserName);

        // Not rates yet — but the settings that decide what "today" and "this financial year"
        // meant for this run, which is what a re-run months later needs to explain a difference.
        run.AttachRatesSnapshot(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timeZone"] = locale.TimeZone.Id,
            ["fiscalYearStartMonth"] = locale.FiscalYearStartMonth,
            ["today"] = context.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["language"] = request.Language == ReportLanguage.Bn ? "bn" : "en",
            ["bengaliNumerals"] = request.BengaliNumerals,
        }, ParameterJsonOptions));

        _runRepository.Add(run);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var subject = await definition.ResolveSubjectAsync(context, cancellationToken).ConfigureAwait(false);

            var farmName = farmId is { } fid
                ? await _environment.DescribeAsync(ReportParameterType.Farm, fid, cancellationToken).ConfigureAwait(false)
                : null;

            // Logo and contact line for the letterhead. Resolved defensively: a report must print
            // even when the organisation has no logo or its file has gone missing.
            var letterhead = await _environment.GetLetterheadAsync(farmId, cancellationToken).ConfigureAwait(false)
                ?? ReportLetterhead.None;

            var meta = new ReportMetaHeader(
                letterhead.OrganizationName ?? _tenantService.TenantName,
                farmName,
                definition.Title.For(request.Language),
                definition.Key,
                run.Id,
                context.UserName,
                nowUtc,
                await DescribeParametersAsync(definition, parameters, context, cancellationToken).ConfigureAwait(false))
            {
                Subject = subject,
                GeneratedAtLocal = ReportTimeZone.FormatStamp(nowUtc, locale.TimeZone),
                GeneratedAtLocalTime = ReportTimeZone.ToLocal(nowUtc, locale.TimeZone),
                TimeZoneLabel = ReportTimeZone.Label(locale.TimeZone, nowUtc),
                ContactLine = letterhead.ContactLine,
                LogoUrl = letterhead.LogoUrl,
                Logo = letterhead.Logo,
            };

            var dataSet = await definition.ExecuteAsync(context, meta, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Report {ReportKey} produced {RowCount} rows in {ElapsedMs}ms (run {RunId}).",
                    definition.Key, dataSet.TotalRowCount, stopwatch.ElapsedMilliseconds, run.Id);
            }

            return (dataSet, run, stopwatch);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            run.Fail(ex.Message, (int)stopwatch.ElapsedMilliseconds);

            // Persist the failed run before rethrowing: a report that blows up is exactly the
            // one someone will ask about later, so the breadcrumb must survive the exception.
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task EnsurePermittedAsync(IReportDefinition definition, CancellationToken cancellationToken)
    {
        if (!await CanRunAsync(definition.Permission, cancellationToken).ConfigureAwait(false))
        {
            throw new ReportAccessDeniedException(definition.Key, definition.Permission ?? string.Empty);
        }
    }

    private async Task<bool> CanRunAsync(string? permission, CancellationToken cancellationToken)
    {
        if (permission is not { Length: > 0 } || _currentUser.IsSystemUser)
        {
            return true;
        }

        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        return await _permissionService
            .HasPermissionAsync(userId, _tenantService.TenantId, permission, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Applies defaults, enforces required parameters, and settles the farm: a blank farm is
    /// filled in only when the user can see exactly one, and rejected otherwise — never silently
    /// replaced by whichever farm happens to sort first.
    /// </summary>
    private async Task<Dictionary<string, string?>> BindAndValidateAsync(
        IReportDefinition definition,
        IReadOnlyDictionary<string, string?> supplied,
        ReportLanguage language,
        CancellationToken cancellationToken)
    {
        var bound = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in definition.Parameters)
        {
            supplied.TryGetValue(parameter.Name, out var value);

            if (string.IsNullOrWhiteSpace(value))
            {
                value = parameter.DefaultValue;
            }

            if (parameter.Type == ReportParameterType.Farm && string.IsNullOrWhiteSpace(value))
            {
                var farms = await _environment.GetAccessibleFarmsAsync(cancellationToken).ConfigureAwait(false);
                if (farms.Count == 1)
                {
                    value = farms[0].Id.ToString();
                }
                else if (parameter.Required)
                {
                    throw new ReportParameterException(
                        parameter.Name,
                        farms.Count == 0
                            ? "You do not have access to any farm, so this report cannot be run."
                            : $"Select a farm: this report covers one farm and you have access to {farms.Count}.");
                }
            }

            if (parameter.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new ReportParameterException(
                    parameter.Name,
                    language == ReportLanguage.Bn
                        ? $"'{parameter.Label.For(language)}' আবশ্যক।"
                        : $"'{parameter.Label.En}' is required.");
            }

            bound[parameter.Name] = value;
        }

        return bound;
    }

    private static Guid? FindFarmId(IReportDefinition definition, Dictionary<string, string?> bound)
    {
        foreach (var parameter in definition.Parameters)
        {
            if (parameter.Type == ReportParameterType.Farm
                && bound.TryGetValue(parameter.Name, out var raw)
                && Guid.TryParse(raw, out var id)
                && id != Guid.Empty)
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// The parameter echo printed on every report. Entities are printed by name (the farm, the
    /// batch, the animal's tag) and date presets as the actual dates they resolved to — a page
    /// that says "Period: last-month" is not evidence of anything.
    /// </summary>
    private async Task<List<ReportMetaField>> DescribeParametersAsync(
        IReportDefinition definition,
        Dictionary<string, string?> bound,
        ReportContext context,
        CancellationToken cancellationToken)
    {
        var language = context.Language;
        var all = ReportStrings.All.For(language);
        var fields = new List<ReportMetaField>(definition.Parameters.Count);

        foreach (var parameter in definition.Parameters)
        {
            bound.TryGetValue(parameter.Name, out var raw);
            string? display;

            switch (parameter.Type)
            {
                case ReportParameterType.Animal or ReportParameterType.Batch or ReportParameterType.Farm
                    or ReportParameterType.Shed or ReportParameterType.Breed or ReportParameterType.InventoryItem:
                    display = Guid.TryParse(raw, out var id) && id != Guid.Empty
                        ? await _environment.DescribeAsync(parameter.Type, id, cancellationToken).ConfigureAwait(false) ?? raw
                        : null;
                    break;

                case ReportParameterType.Select or ReportParameterType.MultiSelect:
                    display = parameter.Options?
                        .FirstOrDefault(o => string.Equals(o.Value, raw, StringComparison.OrdinalIgnoreCase))
                        ?.Label.For(language) ?? raw;
                    break;

                case ReportParameterType.DateRange:
                    display = TryDescribe(() => FormatRange(context.Range(parameter.Name), context), raw);
                    break;

                case ReportParameterType.Date:
                    display = string.IsNullOrWhiteSpace(raw)
                        ? null
                        : TryDescribe(() => FormatDate(context.Date(parameter.Name), context), raw);
                    break;

                case ReportParameterType.Boolean:
                    display = bool.TryParse(raw, out var b)
                        ? (b ? ReportStrings.Yes : ReportStrings.No).For(language)
                        : raw;
                    break;

                default:
                    display = raw;
                    break;
            }

            fields.Add(new ReportMetaField(
                parameter.Label.For(language),
                string.IsNullOrWhiteSpace(display) ? all : display));
        }

        return fields;
    }

    private static string FormatDate(DateOnly date, ReportContext context) =>
        ReportValueFormatter.Format(date, ReportColumnType.Date, 0, context.BengaliNumerals, context.Language);

    private static string FormatRange(DateRange range, ReportContext context) =>
        range.From == range.To
            ? FormatDate(range.From, context)
            : $"{FormatDate(range.From, context)} – {FormatDate(range.To, context)}";

    private static string? TryDescribe(Func<string> describe, string? raw)
    {
        try
        {
            return describe();
        }
        catch (ReportParameterException)
        {
            // The definition will reject the value with a proper message; the echo just shows it.
            return raw;
        }
    }

    private static string BuildFileName(ReportDataSet dataSet, string extension)
    {
        var slug = dataSet.Key.Replace('.', '-');
        var stampTime = dataSet.Meta.GeneratedAtLocalTime ?? dataSet.Meta.GeneratedAtUtc;
        var stamp = stampTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return $"{slug}_{stamp}.{extension}";
    }
}

public sealed class ReportAccessDeniedException : Exception
{
    public ReportAccessDeniedException(string reportKey, string permission)
        : base($"Permission '{permission}' is required to run report '{reportKey}'.")
    {
        ReportKey = reportKey;
        Permission = permission;
    }

    public ReportAccessDeniedException() { }

    public ReportAccessDeniedException(string message) : base(message) { }

    public ReportAccessDeniedException(string message, Exception innerException) : base(message, innerException) { }

    public string ReportKey { get; } = string.Empty;
    public string Permission { get; } = string.Empty;
}

public sealed class ReportTooLargeException : Exception
{
    public ReportTooLargeException(string reportKey, int rowCount, int maxRows)
        : base($"Report '{reportKey}' returned {rowCount:N0} rows, above the {maxRows:N0} row limit. Narrow the filters.")
    {
        ReportKey = reportKey;
        RowCount = rowCount;
    }

    public ReportTooLargeException() { }

    public ReportTooLargeException(string message) : base(message) { }

    public ReportTooLargeException(string message, Exception innerException) : base(message, innerException) { }

    public string ReportKey { get; } = string.Empty;
    public int RowCount { get; }
}
