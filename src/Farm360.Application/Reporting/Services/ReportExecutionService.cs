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
        _logger = logger;
    }

    public IReadOnlyList<ReportDescriptor> GetCatalog() => _registry.All;

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

        var parameters = BindAndValidate(definition, request.Parameters);

        var context = new ReportContext(
            parameters,
            _sender,
            _tenantService.TenantId,
            _currentUser.UserId,
            ResolveUserName(),
            _currentUser.AssignedFarmIds,
            DateOnly.FromDateTime(_dateTime.UtcNow),
            request.Language,
            request.BengaliNumerals);

        var run = ReportRun.Start(
            _tenantService.TenantId,
            definition.Key,
            definition.Title.For(request.Language),
            JsonSerializer.Serialize(parameters, ParameterJsonOptions),
            outputFormat,
            _currentUser.UserId,
            context.UserName);

        _runRepository.Add(run);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var subject = await definition.ResolveSubjectAsync(context, cancellationToken).ConfigureAwait(false);

            var meta = new ReportMetaHeader(
                _tenantService.TenantName,
                null,
                definition.Title.For(request.Language),
                definition.Key,
                run.Id,
                context.UserName,
                _dateTime.UtcNow,
                DescribeParameters(definition, parameters, request.Language))
            {
                Subject = subject,
            };

            var dataSet = await definition.ExecuteAsync(context, meta, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (dataSet.TotalRowCount > MaxRows)
            {
                throw new ReportTooLargeException(definition.Key, dataSet.TotalRowCount, MaxRows);
            }

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
        if (definition.Permission is not { Length: > 0 } permission)
        {
            return;
        }

        if (_currentUser.IsSystemUser)
        {
            return;
        }

        if (_currentUser.UserId is not { } userId)
        {
            throw new ReportAccessDeniedException(definition.Key, permission);
        }

        var allowed = await _permissionService
            .HasPermissionAsync(userId, _tenantService.TenantId, permission, cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            throw new ReportAccessDeniedException(definition.Key, permission);
        }
    }

    private static Dictionary<string, string?> BindAndValidate(
        IReportDefinition definition,
        IReadOnlyDictionary<string, string?> supplied)
    {
        var bound = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in definition.Parameters)
        {
            supplied.TryGetValue(parameter.Name, out var value);

            if (string.IsNullOrWhiteSpace(value))
            {
                value = parameter.DefaultValue;
            }

            if (parameter.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new ReportParameterException(parameter.Name, $"'{parameter.Label.En}' is required.");
            }

            bound[parameter.Name] = value;
        }

        return bound;
    }

    private static List<ReportMetaField> DescribeParameters(
        IReportDefinition definition,
        Dictionary<string, string?> bound,
        ReportLanguage language)
    {
        var fields = new List<ReportMetaField>(definition.Parameters.Count);

        foreach (var parameter in definition.Parameters)
        {
            bound.TryGetValue(parameter.Name, out var raw);

            // Identifiers say nothing to a reader; the Subject line carries the real identity.
            if (parameter.Type is ReportParameterType.Animal or ReportParameterType.Batch
                or ReportParameterType.Farm or ReportParameterType.Shed or ReportParameterType.Breed)
            {
                continue;
            }

            var display = parameter.Type switch
            {
                ReportParameterType.Select or ReportParameterType.MultiSelect =>
                    parameter.Options?.FirstOrDefault(o => string.Equals(o.Value, raw, StringComparison.OrdinalIgnoreCase))
                        ?.Label.For(language) ?? raw,
                _ => raw,
            };

            fields.Add(new ReportMetaField(
                parameter.Label.For(language),
                string.IsNullOrWhiteSpace(display) ? "All" : display));
        }

        return fields;
    }

    private string ResolveUserName()
    {
        if (_currentUser.IsSystemUser)
        {
            return "system";
        }

        return _currentUser.Role is { Length: > 0 } role
            ? $"{_currentUser.UserId} ({role})"
            : _currentUser.UserId?.ToString() ?? "anonymous";
    }

    private static string BuildFileName(ReportDataSet dataSet, string extension)
    {
        var slug = dataSet.Key.Replace('.', '-');
        var stamp = dataSet.Meta.GeneratedAtUtc.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
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
