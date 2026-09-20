using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Model;
using MediatR;

namespace Farm360.Application.Reporting.Abstractions;

/// <summary>
/// Everything a report definition is allowed to see while it fetches data: its bound parameters,
/// a mediator for reusing existing queries, and the resolved tenant/farm scope.
/// </summary>
/// <remarks>
/// Definitions never touch <c>DbContext</c> directly. They go through MediatR, which means the
/// tenant query filter and every existing validation rule apply unchanged — a report cannot
/// become a side door around the rules the rest of the application follows.
/// </remarks>
public sealed class ReportContext
{
    private readonly IReadOnlyDictionary<string, string?> _parameters;
    private readonly ISender _sender;

    public ReportContext(
        IReadOnlyDictionary<string, string?> parameters,
        ISender sender,
        Guid tenantId,
        Guid? userId,
        string userName,
        IReadOnlyList<Guid>? assignedFarmIds,
        DateOnly today,
        ReportLanguage language,
        bool bengaliNumerals)
    {
        _parameters = parameters;
        _sender = sender;
        TenantId = tenantId;
        UserId = userId;
        UserName = userName;
        AssignedFarmIds = assignedFarmIds;
        Today = today;
        Language = language;
        BengaliNumerals = bengaliNumerals;
    }

    public Guid TenantId { get; }
    public Guid? UserId { get; }
    public string UserName { get; }

    /// <summary>Null means "every farm in the tenant" — the same convention as ICurrentUserService.</summary>
    public IReadOnlyList<Guid>? AssignedFarmIds { get; }

    public DateOnly Today { get; }
    public ReportLanguage Language { get; }
    public bool BengaliNumerals { get; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        _sender.Send(request, cancellationToken);

    // ── Typed parameter access ──────────────────────────────────────────────

    public string? Raw(string name) => _parameters.TryGetValue(name, out var v) ? v : null;

    public bool Has(string name) => !string.IsNullOrWhiteSpace(Raw(name));

    public string Text(string name, string fallback = "") => Raw(name) is { Length: > 0 } s ? s : fallback;

    public Guid Id(string name)
    {
        var raw = Raw(name);
        if (System.Guid.TryParse(raw, out var id) && id != System.Guid.Empty)
        {
            return id;
        }

        throw new ReportParameterException(name, $"'{name}' must be a valid identifier.");
    }

    public Guid? IdOrNull(string name) =>
        System.Guid.TryParse(Raw(name), out var id) && id != System.Guid.Empty ? id : null;

    public bool Bool(string name, bool fallback = false) =>
        bool.TryParse(Raw(name), out var b) ? b : fallback;

    public int WholeNumber(string name, int fallback = 0) =>
        int.TryParse(Raw(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

    public decimal Number(string name, decimal fallback = 0m) =>
        decimal.TryParse(Raw(name), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    public DateOnly Date(string name)
    {
        var raw = Raw(name);
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "today", StringComparison.OrdinalIgnoreCase))
        {
            return Today;
        }

        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        throw new ReportParameterException(name, $"'{name}' must be a date (yyyy-MM-dd).");
    }

    /// <summary>
    /// Accepts "yyyy-MM-dd..yyyy-MM-dd" or one of the presets the parameter panel offers.
    /// </summary>
    public DateRange Range(string name)
    {
        var raw = Raw(name);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateRange.CurrentMonth(Today);
        }

        var preset = ResolvePreset(raw);
        if (preset is not null)
        {
            return preset.Value;
        }

        var parts = raw.Split("..", StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && DateOnly.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            && DateOnly.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
        {
            return from <= to
                ? new DateRange(from, to)
                : throw new ReportParameterException(name, $"'{name}' start date must not be after the end date.");
        }

        throw new ReportParameterException(name, $"'{name}' must be a range 'yyyy-MM-dd..yyyy-MM-dd' or a preset.");
    }

    private DateRange? ResolvePreset(string raw) => raw.ToLowerInvariant() switch
    {
        "today" => new DateRange(Today, Today),
        "current-month" => DateRange.CurrentMonth(Today),
        "last-month" => DateRange.CurrentMonth(Today.AddMonths(-1)),
        "last-7-days" => DateRange.LastDays(Today, 7),
        "last-30-days" => DateRange.LastDays(Today, 30),
        "last-90-days" => DateRange.LastDays(Today, 90),
        "current-year" => DateRange.CurrentYear(Today),
        "all-time" => new DateRange(new DateOnly(2000, 1, 1), Today),
        _ => null,
    };
}

/// <summary>Raised when a supplied parameter cannot be bound. Surfaces as HTTP 400, not 500.</summary>
public sealed class ReportParameterException : Exception
{
    public ReportParameterException(string parameterName, string message) : base(message)
    {
        ParameterName = parameterName;
    }

    public ReportParameterException() { }

    public ReportParameterException(string message) : base(message) { }

    public ReportParameterException(string message, Exception innerException) : base(message, innerException) { }

    public string ParameterName { get; } = string.Empty;
}
