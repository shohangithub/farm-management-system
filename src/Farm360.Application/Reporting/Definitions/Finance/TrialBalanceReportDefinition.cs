using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record TrialBalanceReportRow(
    string AccountCode,
    string AccountName,
    string Category,
    decimal DebitBdt,
    decimal CreditBdt);

/// <summary>
/// Report FN1 — Trial Balance statement verifying double-entry mathematical equilibrium across
/// asset, liability, equity, revenue, and expense accounts.
/// </summary>
public sealed class TrialBalanceReportDefinition : ReportDefinition<TrialBalanceReportRow>
{
    public override string Key => "finance.trial-balance";

    public override LocalizedText Title => new("Trial Balance Report", "রেওয়ামিল প্রতিবেদন");

    public override LocalizedText Description => new(
        "Summary of debit and credit ledger balances ensuring double-entry mathematical integrity.",
        "সম্পদ, দায়, মালিকানা স্বত্ব, আয় ও ব্যয়ের খতিয়ান উদ্বৃত্তের গাণিতিক শুদ্ধতা যাচাইকরণ বিবরণী।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Date("asOfDate", new LocalizedText("As of Date", "তারিখ অনুযায়ী"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<TrialBalanceReportRow>> Columns =>
    [
        ReportColumn<TrialBalanceReportRow>.Text("code", new LocalizedText("Account Code", "হিসাব কোড"), r => r.AccountCode, width: 22f, relative: false),
        ReportColumn<TrialBalanceReportRow>.Text("name", new LocalizedText("Account Name", "হিসাবের নাম"), r => r.AccountName, width: 3.0f),
        ReportColumn<TrialBalanceReportRow>.Text("category", new LocalizedText("Category", "শ্রেণী"), r => r.Category, width: 24f, relative: false),
        ReportColumn<TrialBalanceReportRow>.Money("debit", new LocalizedText("Debit (BDT)", "ডেবিট (টাকা)"), r => r.DebitBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<TrialBalanceReportRow>.Money("credit", new LocalizedText("Credit (BDT)", "ক্রেডিট (টাকা)"), r => r.CreditBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<TrialBalanceReportRow>? Group =>
        ReportGroup<TrialBalanceReportRow>.By(
            r => r.Category,
            r => $"{r.Category} Accounts");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var asOf = context.Date("asOfDate");
        return $"Statement of Ledger Balances as of {asOf:dd-MMM-yyyy}";
    }

    protected override async Task<IReadOnlyList<TrialBalanceReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var asOfDate = context.Date("asOfDate").ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var data = await context
            .Send(new GetTrialBalanceQuery(farmId, asOfDate), cancellationToken)
            .ConfigureAwait(false);

        if (data?.Lines == null || data.Lines.Count == 0)
        {
            return [];
        }

        return data.Lines
            .Select(l => new TrialBalanceReportRow(
                AccountCode: l.AccountCode,
                AccountName: l.AccountName,
                Category: l.CategoryGroup,
                DebitBdt: l.DebitBdt,
                CreditBdt: l.CreditBdt))
            .ToList();
    }

    private static async Task<Guid> ResolveFarmIdAsync(ReportContext context, CancellationToken cancellationToken)
    {
        if (context.IdOrNull("farmId") is { } id && id != Guid.Empty)
            return id;
        if (context.AssignedFarmIds is { Count: > 0 } assigned && assigned[0] != Guid.Empty)
            return assigned[0];

        var farms = await context.Send(new GetAllFarmsQuery(), cancellationToken).ConfigureAwait(false);
        return farms.Count > 0 ? farms[0].Id : Guid.Empty;
    }
}
