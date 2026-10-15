using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Commands;

// ═══ Accounting settings ═══════════════════════════════════════════════════

/// <summary>
/// Saves the farm's financial year and books start date. Moving the books start re-dates the
/// opening balances and opening fixed assets with it, since both describe the position on that day.
/// </summary>
public record SaveAccountingSettingsCommand(Guid TenantId, Guid FarmId, SaveAccountingSettingsRequest Request)
    : IRequest<AccountingSettingsDto>, ITransactionalCommand;

public class SaveAccountingSettingsCommandHandler(
    IFarmAccountingSettingsRepository settings,
    IBalanceSheetEntryRepository entries,
    IFixedAssetRepository assets,
    IUnitOfWork unitOfWork) : IRequestHandler<SaveAccountingSettingsCommand, AccountingSettingsDto>
{
    public async Task<AccountingSettingsDto> Handle(SaveAccountingSettingsCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var booksStart = request.BooksStartDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;

        var existing = await settings.GetByFarmIdAsync(command.FarmId, cancellationToken);
        if (existing is null)
        {
            existing = FarmAccountingSettings.Create(command.TenantId, command.FarmId, request.FiscalYearStartMonth, booksStart);
            settings.Add(existing);
        }
        else
        {
            var previousStart = existing.BooksStartDate;
            existing.Update(request.FiscalYearStartMonth, booksStart);
            settings.Update(existing);

            if (previousStart != booksStart)
                await MoveOpeningPositionAsync(command.FarmId, booksStart, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return existing.ToDto();
    }

    private async Task MoveOpeningPositionAsync(Guid farmId, DateOnly? booksStart, CancellationToken cancellationToken)
    {
        var openingEntries = (await entries.GetByFarmIdAsync(farmId, cancellationToken))
            .Where(e => e.EntryType == BalanceSheetEntryType.Opening)
            .ToList();
        var openingAssets = (await assets.GetByFarmIdAsync(farmId, cancellationToken))
            .Where(a => a.Acquisition == FixedAssetAcquisition.Opening)
            .ToList();

        if (booksStart is not { } start)
        {
            if (openingEntries.Count > 0 || openingAssets.Count > 0)
                throw new ConflictException("Opening balances and opening fixed assets need a books start date. Delete them before clearing it.");
            return;
        }

        foreach (var id in openingEntries.Select(e => e.Id))
        {
            var entry = await entries.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("BalanceSheetEntry", id);
            entry.Update(entry.EntryType, start, entry.AccountCode, entry.Amount, entry.Description, null, null, entry.Notes);
            entries.Update(entry);
        }

        foreach (var id in openingAssets.Select(a => a.Id))
        {
            var asset = await assets.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException("FixedAsset", id);
            asset.Update(asset.Name, asset.Category, start, asset.Cost, asset.Method, asset.RatePercent,
                asset.UsefulLifeYears, asset.ResidualValue, asset.Location, asset.Notes);
            assets.Update(asset);
        }
    }
}

// ═══ Opening balances & adjustments ════════════════════════════════════════

public record CreateBalanceSheetEntryCommand(Guid TenantId, Guid FarmId, SaveBalanceSheetEntryRequest Request)
    : IRequest<BalanceSheetEntryDto>;

public record UpdateBalanceSheetEntryCommand(Guid FarmId, Guid EntryId, SaveBalanceSheetEntryRequest Request)
    : IRequest<BalanceSheetEntryDto>;

public record DeleteBalanceSheetEntryCommand(Guid FarmId, Guid EntryId) : IRequest;

internal static class BalanceSheetEntryRules
{
    /// <summary>Validates a request against the chart and the books start; returns the parsed values.</summary>
    public static async Task<(BalanceSheetEntryType Type, DateOnly Date)> CheckAsync(
        IFarmAccountingSettingsRepository settings,
        Guid farmId,
        SaveBalanceSheetEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BalanceSheetEntryType>(request.EntryType, true, out var type))
            throw new ArgumentException($"Invalid entry type: {request.EntryType}");

        var booksStart = (await settings.GetByFarmIdAsync(farmId, cancellationToken))?.BooksStartDate;

        if (type == BalanceSheetEntryType.Opening)
        {
            if (booksStart is not { } start)
                throw new ArgumentException("Set the books start date in the accounting settings before entering opening balances.");
            if (!AccountChart.OpeningEntryAccounts.Contains(request.AccountCode))
                throw new ArgumentException($"Account {request.AccountCode} cannot take an opening balance. Fixed assets are entered in the fixed-asset register.");
            // Opening balances are the position on the books start date, whatever date was sent.
            return (type, start);
        }

        if (!AccountChart.AdjustmentAccounts.Contains(request.AccountCode))
            throw new ArgumentException($"Account {request.AccountCode} cannot be adjusted here. Use prepayments, provisions or other liabilities.");
        if (request.ProfitAccountCode is null || AccountChart.ProfitAndLossAccounts.All(a => a.Code != request.ProfitAccountCode))
            throw new ArgumentException("Choose the income or expense account the adjustment affects.");

        var date = DateOnly.FromDateTime(request.EntryDate);
        if (booksStart is { } bs && date <= bs)
            throw new ArgumentException($"Adjustments must be dated after the books start ({bs:dd-MMM-yyyy}).");
        return (type, date);
    }

    public static DateOnly? Reversal(SaveBalanceSheetEntryRequest request) =>
        request.ReversalDate is { } r ? DateOnly.FromDateTime(r) : null;
}

public class CreateBalanceSheetEntryCommandHandler(
    IBalanceSheetEntryRepository entries,
    IFarmAccountingSettingsRepository settings,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateBalanceSheetEntryCommand, BalanceSheetEntryDto>
{
    public async Task<BalanceSheetEntryDto> Handle(CreateBalanceSheetEntryCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        var (type, date) = await BalanceSheetEntryRules.CheckAsync(settings, command.FarmId, r, cancellationToken);

        var next = await entries.GetLastCodeNumberAsync(command.FarmId, cancellationToken) + 1;
        var prefix = type == BalanceSheetEntryType.Opening ? "OB" : "ADJ";
        var entry = BalanceSheetEntry.Create(
            command.TenantId, command.FarmId, $"{prefix}-{next:0000}", type, date,
            r.AccountCode, r.AmountBdt, r.Description, r.ProfitAccountCode, BalanceSheetEntryRules.Reversal(r), r.Notes);

        entries.Add(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entry.ToDto();
    }
}

public class UpdateBalanceSheetEntryCommandHandler(
    IBalanceSheetEntryRepository entries,
    IFarmAccountingSettingsRepository settings,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateBalanceSheetEntryCommand, BalanceSheetEntryDto>
{
    public async Task<BalanceSheetEntryDto> Handle(UpdateBalanceSheetEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await entries.GetByIdAsync(command.EntryId, cancellationToken);
        if (entry is null || entry.FarmId != command.FarmId)
            throw new NotFoundException("BalanceSheetEntry", command.EntryId);

        var r = command.Request;
        var (type, date) = await BalanceSheetEntryRules.CheckAsync(settings, command.FarmId, r, cancellationToken);
        entry.Update(type, date, r.AccountCode, r.AmountBdt, r.Description, r.ProfitAccountCode, BalanceSheetEntryRules.Reversal(r), r.Notes);

        entries.Update(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entry.ToDto();
    }
}

public class DeleteBalanceSheetEntryCommandHandler(IBalanceSheetEntryRepository entries, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteBalanceSheetEntryCommand>
{
    public async Task Handle(DeleteBalanceSheetEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await entries.GetByIdAsync(command.EntryId, cancellationToken);
        if (entry is null || entry.FarmId != command.FarmId)
            throw new NotFoundException("BalanceSheetEntry", command.EntryId);

        entries.Remove(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Moving onto the statements part-way through: turns every posting up to the books start into
/// opening balances, so nothing recorded earlier is lost. Profit and loss to that date becomes
/// opening retained earnings; drawings reduce owner's capital; distributions reduce retained earnings.
/// </summary>
public record GenerateOpeningEntriesCommand(Guid TenantId, Guid FarmId)
    : IRequest<GenerateOpeningEntriesResultDto>, ITransactionalCommand;

public record GenerateOpeningEntriesResultDto(int Created, IReadOnlyList<string> Warnings);

public class GenerateOpeningEntriesCommandHandler(
    IFinancialTransactionRepository transactions,
    IBalanceSheetEntryRepository entries,
    IFarmAccountingSettingsRepository settings,
    IUnitOfWork unitOfWork) : IRequestHandler<GenerateOpeningEntriesCommand, GenerateOpeningEntriesResultDto>
{
    public async Task<GenerateOpeningEntriesResultDto> Handle(GenerateOpeningEntriesCommand command, CancellationToken cancellationToken)
    {
        var start = (await settings.GetByFarmIdAsync(command.FarmId, cancellationToken))?.BooksStartDate
            ?? throw new ArgumentException("Set the books start date first.");

        var existing = await entries.GetByFarmIdAsync(command.FarmId, cancellationToken);
        if (existing.Any(e => e.EntryType == BalanceSheetEntryType.Opening))
            throw new ConflictException("Opening balances already exist. Delete them first to regenerate from the ledger.");

        var postings = (await transactions.GetAllByFarmIdAsync(command.FarmId, cancellationToken))
            .Where(t => DateOnly.FromDateTime(t.TransactionDate) <= start)
            .ToList();
        var balances = AccountChart.Balances(postings);
        decimal Bal(string code) => balances.GetValueOrDefault(code);

        // Natural-side amounts per opening line.
        var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var code in AccountChart.OpeningEntryAccounts)
            amounts[code] = AccountChart.IsDebitNature(code) ? Bal(code) : -Bal(code);

        var profit = AccountChart.NetProfit(postings);
        amounts[AccountChart.RetainedEarningsAccount] += profit - Bal(AccountChart.ProfitDistributionAccount);
        amounts[AccountChart.OwnerCapitalAccount] -= Bal(AccountChart.OwnerDrawingAccount);

        var warnings = new List<string>();
        if (Bal(AccountChart.FixedAssetAccount) != 0m)
        {
            warnings.Add($"Fixed assets of {Bal(AccountChart.FixedAssetAccount):N2} BDT were bought before the books start. Register them as opening fixed assets at their written-down value.");
        }

        var next = await entries.GetLastCodeNumberAsync(command.FarmId, cancellationToken);
        var created = 0;
        foreach (var (code, amount) in amounts)
        {
            var rounded = Math.Round(amount, 2);
            if (rounded == 0m)
                continue;

            next++;
            entries.Add(BalanceSheetEntry.Create(
                command.TenantId, command.FarmId, $"OB-{next:0000}", BalanceSheetEntryType.Opening, start,
                code, rounded, $"Opening balance from the ledger — {AccountChart.Account(code).Name}"));
            created++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new GenerateOpeningEntriesResultDto(created, warnings);
    }
}

// ═══ Fixed assets ══════════════════════════════════════════════════════════

public record CreateFixedAssetCommand(Guid TenantId, Guid FarmId, SaveFixedAssetRequest Request)
    : IRequest<FixedAssetDto>, ITransactionalCommand;

public record UpdateFixedAssetCommand(Guid TenantId, Guid FarmId, Guid AssetId, SaveFixedAssetRequest Request)
    : IRequest<FixedAssetDto>, ITransactionalCommand;

public record DeleteFixedAssetCommand(Guid FarmId, Guid AssetId) : IRequest, ITransactionalCommand;

public record DisposeFixedAssetCommand(Guid TenantId, Guid FarmId, Guid AssetId, DisposeFixedAssetRequest Request)
    : IRequest<FixedAssetDto>, ITransactionalCommand;

public record CancelFixedAssetDisposalCommand(Guid TenantId, Guid FarmId, Guid AssetId)
    : IRequest<FixedAssetDto>, ITransactionalCommand;

/// <summary>How a fixed asset reaches the ledger.</summary>
/// <remarks>
/// Cash: Dr PPE / Cr Cash. Credit: the same, put on a supplier bill (Cr Accounts Payable).
/// Owner contributed: Dr PPE / Cr Owner's capital. Opening: no posting — its written-down value is
/// part of the opening position. Sale proceeds: Dr Cash / Cr 5410, against which the statements
/// derecognise the net book value, leaving the gain or loss on disposal.
/// </remarks>
internal static class FixedAssetPostings
{
    public const string Module = "FixedAssets";

    public static TransactionCategory AcquisitionCategory(FixedAssetAcquisition acquisition) =>
        acquisition == FixedAssetAcquisition.OwnerContributed
            ? TransactionCategory.OwnerCapital
            : TransactionCategory.FixedAssetPurchase;

    public static async Task<int> FiscalMonthAsync(IFarmAccountingSettingsRepository settings, Guid farmId, CancellationToken ct) =>
        (await settings.GetByFarmIdAsync(farmId, ct))?.FiscalYearStartMonth ?? FiscalYear.DefaultStartMonth;

    public static async Task<(FixedAssetCategory Category, DepreciationMethod Method, DateOnly Date)> CheckAsync(
        IFarmAccountingSettingsRepository settings,
        Guid farmId,
        FixedAssetAcquisition acquisition,
        SaveFixedAssetRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FixedAssetCategory>(request.Category, true, out var category))
            throw new ArgumentException($"Invalid asset category: {request.Category}");
        if (!Enum.TryParse<DepreciationMethod>(request.Method, true, out var method))
            throw new ArgumentException($"Invalid depreciation method: {request.Method}");

        var booksStart = (await settings.GetByFarmIdAsync(farmId, cancellationToken))?.BooksStartDate;
        if (acquisition == FixedAssetAcquisition.Opening)
        {
            if (booksStart is not { } start)
                throw new ArgumentException("Set the books start date before registering assets owned before it.");
            return (category, method, start);
        }

        var date = DateOnly.FromDateTime(request.AcquisitionDate);
        if (booksStart is { } bs && date <= bs)
            throw new ArgumentException($"Bought on or before the books start ({bs:dd-MMM-yyyy}): register it as an opening asset at its written-down value.");
        return (category, method, date);
    }

    public static Task<FinancialTransaction?> PostAcquisitionAsync(ILedgerPostingService ledger, FixedAsset asset, CancellationToken ct)
    {
        if (asset.Acquisition == FixedAssetAcquisition.Opening)
            return Task.FromResult<FinancialTransaction?>(null);

        var owner = asset.Acquisition == FixedAssetAcquisition.OwnerContributed;
        return ledger.PostAsync(new LedgerPosting(
            asset.TenantId,
            asset.FarmId,
            AcquisitionCategory(asset.Acquisition),
            asset.Cost,
            asset.AcquisitionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            asset.Id,
            Module,
            owner ? $"Fixed asset brought in by owner — {asset.Name}" : $"Fixed asset purchased — {asset.Name}",
            Settlement: owner ? SettlementAccount.FixedAsset : SettlementAccount.Cash,
            ReferenceId: asset.Code), ct);
    }

    public static Task PostDisposalAsync(ILedgerPostingService ledger, FixedAsset asset, CancellationToken ct) =>
        // Zero proceeds (scrapped, or disposal cancelled) removes the posting.
        ledger.PostAsync(new LedgerPosting(
            asset.TenantId,
            asset.FarmId,
            TransactionCategory.AssetDisposal,
            asset.DisposalDate is null ? 0m : asset.DisposalProceeds,
            (asset.DisposalDate ?? asset.AcquisitionDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            asset.Id,
            Module,
            $"Sale of fixed asset — {asset.Name}",
            ReferenceId: asset.Code), ct);
}

public class CreateFixedAssetCommandHandler(
    IFixedAssetRepository assets,
    IFarmAccountingSettingsRepository settings,
    ILedgerPostingService ledger,
    ITradeCreditService credit,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFixedAssetCommand, FixedAssetDto>
{
    public async Task<FixedAssetDto> Handle(CreateFixedAssetCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        if (!Enum.TryParse<FixedAssetAcquisition>(r.Acquisition, true, out var acquisition))
            throw new ArgumentException($"Invalid acquisition: {r.Acquisition}");
        if (acquisition == FixedAssetAcquisition.Credit && r.Credit is null)
            throw new ArgumentException("Enter the supplier for an asset bought on credit.");

        var (category, method, date) = await FixedAssetPostings.CheckAsync(settings, command.FarmId, acquisition, r, cancellationToken);
        var next = await assets.GetLastCodeNumberAsync(command.FarmId, cancellationToken) + 1;
        var asset = FixedAsset.Create(
            command.TenantId, command.FarmId, $"FA-{next:0000}", r.Name, category, acquisition, date, r.CostBdt,
            method, r.RatePercent, r.UsefulLifeYears, r.ResidualValueBdt, r.Location, r.Notes);
        assets.Add(asset);

        var posting = await FixedAssetPostings.PostAcquisitionAsync(ledger, asset, cancellationToken);
        if (acquisition == FixedAssetAcquisition.Credit && posting is not null)
        {
            var c = r.Credit!;
            await credit.OpenAsync(posting,
                new CreditTerms(c.PartyName, c.PartyPhone, c.DueDate, c.PaidNowBdt, c.PaymentMethod, c.Notes), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return asset.ToDto(DateOnly.FromDateTime(DateTime.UtcNow), await FixedAssetPostings.FiscalMonthAsync(settings, command.FarmId, cancellationToken));
    }
}

public class UpdateFixedAssetCommandHandler(
    IFixedAssetRepository assets,
    IFarmAccountingSettingsRepository settings,
    ILedgerPostingService ledger,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateFixedAssetCommand, FixedAssetDto>
{
    public async Task<FixedAssetDto> Handle(UpdateFixedAssetCommand command, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(command.AssetId, cancellationToken);
        if (asset is null || asset.FarmId != command.FarmId)
            throw new NotFoundException("FixedAsset", command.AssetId);

        var r = command.Request;
        var (category, method, date) = await FixedAssetPostings.CheckAsync(settings, command.FarmId, asset.Acquisition, r, cancellationToken);
        asset.Update(r.Name, category, date, r.CostBdt, method, r.RatePercent, r.UsefulLifeYears, r.ResidualValueBdt, r.Location, r.Notes);
        assets.Update(asset);

        // Re-posting revises the existing posting (and its supplier bill, if bought on credit).
        await FixedAssetPostings.PostAcquisitionAsync(ledger, asset, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return asset.ToDto(DateOnly.FromDateTime(DateTime.UtcNow), await FixedAssetPostings.FiscalMonthAsync(settings, command.FarmId, cancellationToken));
    }
}

public class DeleteFixedAssetCommandHandler(
    IFixedAssetRepository assets,
    ILedgerPostingService ledger,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteFixedAssetCommand>
{
    public async Task Handle(DeleteFixedAssetCommand command, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(command.AssetId, cancellationToken);
        if (asset is null || asset.FarmId != command.FarmId)
            throw new NotFoundException("FixedAsset", command.AssetId);

        await ledger.ReverseAsync(command.FarmId, asset.Id, FixedAssetPostings.AcquisitionCategory(asset.Acquisition), cancellationToken);
        await ledger.ReverseAsync(command.FarmId, asset.Id, TransactionCategory.AssetDisposal, cancellationToken);
        assets.Remove(asset);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public class DisposeFixedAssetCommandHandler(
    IFixedAssetRepository assets,
    IFarmAccountingSettingsRepository settings,
    ILedgerPostingService ledger,
    IUnitOfWork unitOfWork) : IRequestHandler<DisposeFixedAssetCommand, FixedAssetDto>
{
    public async Task<FixedAssetDto> Handle(DisposeFixedAssetCommand command, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(command.AssetId, cancellationToken);
        if (asset is null || asset.FarmId != command.FarmId)
            throw new NotFoundException("FixedAsset", command.AssetId);

        asset.RecordDisposal(DateOnly.FromDateTime(command.Request.DisposalDate), command.Request.ProceedsBdt);
        assets.Update(asset);
        await FixedAssetPostings.PostDisposalAsync(ledger, asset, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return asset.ToDto(DateOnly.FromDateTime(DateTime.UtcNow), await FixedAssetPostings.FiscalMonthAsync(settings, command.FarmId, cancellationToken));
    }
}

public class CancelFixedAssetDisposalCommandHandler(
    IFixedAssetRepository assets,
    IFarmAccountingSettingsRepository settings,
    ILedgerPostingService ledger,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelFixedAssetDisposalCommand, FixedAssetDto>
{
    public async Task<FixedAssetDto> Handle(CancelFixedAssetDisposalCommand command, CancellationToken cancellationToken)
    {
        var asset = await assets.GetByIdAsync(command.AssetId, cancellationToken);
        if (asset is null || asset.FarmId != command.FarmId)
            throw new NotFoundException("FixedAsset", command.AssetId);

        asset.CancelDisposal();
        assets.Update(asset);
        await FixedAssetPostings.PostDisposalAsync(ledger, asset, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return asset.ToDto(DateOnly.FromDateTime(DateTime.UtcNow), await FixedAssetPostings.FiscalMonthAsync(settings, command.FarmId, cancellationToken));
    }
}
