using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Services;
using Farm360.Domain.BusinessRules;

namespace Farm360.Application.Documents;

/// <summary>Builds the letterhead and language settings a customer document prints with.</summary>
public interface IDocumentHeaderBuilder
{
    /// <param name="farmId">The farm the document belongs to (its letterhead, zone and rules).</param>
    /// <param name="language">"en" or "bn"; blank means the farm's "Default report language" rule.</param>
    /// <param name="bengaliNumerals">Null means the farm's "Bengali numerals" rule.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<DocumentHeader> BuildAsync(Guid farmId, string? language, bool? bengaliNumerals, CancellationToken cancellationToken);
}

/// <summary>
/// Reuses the report platform's environment — the same letterhead, logo, time zone and user name
/// a report prints — so a farm's invoices and its reports carry one identity.
/// </summary>
public sealed class DocumentHeaderBuilder : IDocumentHeaderBuilder
{
    private readonly IReportEnvironment _environment;
    private readonly IFarmBusinessRulesProvider _rules;
    private readonly ITenantService _tenant;
    private readonly IDateTimeService _clock;

    public DocumentHeaderBuilder(
        IReportEnvironment environment,
        IFarmBusinessRulesProvider rules,
        ITenantService tenant,
        IDateTimeService clock)
    {
        _environment = environment;
        _rules = rules;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<DocumentHeader> BuildAsync(Guid farmId, string? language, bool? bengaliNumerals, CancellationToken cancellationToken)
    {
        var rules = _rules.For(_tenant.TenantId, farmId);
        var resolvedLanguage = ResolveLanguage(language, rules.DefaultReportLanguage);
        var resolvedNumerals = bengaliNumerals ?? rules.BengaliNumeralsByDefault;

        var letterhead = await _environment.GetLetterheadAsync(farmId, cancellationToken).ConfigureAwait(false)
            ?? ReportLetterhead.None;
        var farmName = await _environment.DescribeAsync(ReportParameterType.Farm, farmId, cancellationToken).ConfigureAwait(false);
        var locale = await _environment.GetLocaleAsync(farmId, cancellationToken).ConfigureAwait(false)
            ?? ReportLocale.Default;
        var userName = await _environment.GetUserDisplayNameAsync(cancellationToken).ConfigureAwait(false);

        var nowUtc = _clock.UtcNow;

        return new DocumentHeader(
            letterhead.OrganizationName ?? (string.IsNullOrWhiteSpace(_tenant.TenantName) ? "Farm360" : _tenant.TenantName),
            farmName,
            letterhead.ContactLine,
            letterhead.Logo,
            resolvedLanguage,
            resolvedNumerals,
            string.IsNullOrWhiteSpace(userName) ? "—" : userName,
            ReportTimeZone.FormatStamp(nowUtc, locale.TimeZone),
            ReportTimeZone.Today(nowUtc, locale.TimeZone));
    }

    /// <summary>"bn" / "bangla" / "বাংলা" → Bangla, "en" / "english" → English, anything else → the farm rule.</summary>
    public static ReportLanguage ResolveLanguage(string? requested, ReportLanguagePreference farmDefault)
    {
        var value = requested?.Trim();
        if (string.Equals(value, "bn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "bangla", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "বাংলা", StringComparison.Ordinal))
        {
            return ReportLanguage.Bn;
        }

        if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "english", StringComparison.OrdinalIgnoreCase))
        {
            return ReportLanguage.En;
        }

        return farmDefault == ReportLanguagePreference.Bangla ? ReportLanguage.Bn : ReportLanguage.En;
    }
}
