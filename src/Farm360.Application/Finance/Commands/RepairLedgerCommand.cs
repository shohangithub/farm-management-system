using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Finance.Queries
{
    /// <summary>Reconciles the books against stock, herd, loan and investor registers and lists issues.</summary>
    public record GetAccountingHealthQuery(Guid FarmId) : IRequest<AccountingHealthDto>;

    public class GetAccountingHealthQueryHandler : IRequestHandler<GetAccountingHealthQuery, AccountingHealthDto>
    {
        private readonly IAccountingReconciliationService _reconciliation;

        public GetAccountingHealthQueryHandler(IAccountingReconciliationService reconciliation)
        {
            _reconciliation = reconciliation;
        }

        public Task<AccountingHealthDto> Handle(GetAccountingHealthQuery request, CancellationToken cancellationToken) =>
            _reconciliation.CheckAsync(request.FarmId, cancellationToken);
    }
}

namespace Farm360.Application.Finance.Commands
{
    /// <summary>
    /// Brings records made before the accounting overhaul into line with the current rules. Safe to run
    /// any number of times: every step is idempotent and only posts what is missing or wrong.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Loans: posts the money received for loans that were never posted, and splits old
    /// repayments into principal and interest.</item>
    /// <item>Animals: re-dates purchases to the acquisition date, corrects amounts to the herd
    /// register, and releases the cost of sold/dead animals from the livestock asset.</item>
    /// <item>Inventory (only when asked, after a physical count): adjusts the books to the stock on hand.</item>
    /// </list>
    /// </remarks>
    public record RepairLedgerCommand(Guid TenantId, Guid FarmId, bool AdjustInventoryToStockCount) : IRequest<LedgerRepairResultDto>;

    public class RepairLedgerCommandHandler : IRequestHandler<RepairLedgerCommand, LedgerRepairResultDto>
    {
        private readonly IFinancialTransactionRepository _transactions;
        private readonly ILoanRecordRepository _loans;
        private readonly IAnimalRepository _animals;
        private readonly ILedgerPostingService _posting;
        private readonly ILivestockAccountingService _livestock;
        private readonly IAccountingReconciliationService _reconciliation;
        private readonly IUnitOfWork _unitOfWork;

        public RepairLedgerCommandHandler(
            IFinancialTransactionRepository transactions,
            ILoanRecordRepository loans,
            IAnimalRepository animals,
            ILedgerPostingService posting,
            ILivestockAccountingService livestock,
            IAccountingReconciliationService reconciliation,
            IUnitOfWork unitOfWork)
        {
            _transactions = transactions;
            _loans = loans;
            _animals = animals;
            _posting = posting;
            _livestock = livestock;
            _reconciliation = reconciliation;
            _unitOfWork = unitOfWork;
        }

        public async Task<LedgerRepairResultDto> Handle(RepairLedgerCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var messages = new List<string>();

            var (disbursements, splits) = await RepairLoansAsync(request, messages, cancellationToken);
            var (resynced, disposals) = await RepairAnimalsAsync(request.FarmId, messages, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var adjustment = 0m;
            if (request.AdjustInventoryToStockCount)
            {
                adjustment = await AdjustInventoryAsync(request, messages, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (messages.Count == 0)
                messages.Add("Nothing to repair — the ledger already follows the accounting rules.");

            var health = await _reconciliation.CheckAsync(request.FarmId, cancellationToken);
            return new LedgerRepairResultDto(disbursements, splits, resynced, disposals, adjustment, messages, health);
        }

        private async Task<(int Disbursements, int Splits)> RepairLoansAsync(
            RepairLedgerCommand request, List<string> messages, CancellationToken cancellationToken)
        {
            var loans = await _loans.GetByFarmIdAsync(request.FarmId, cancellationToken);
            var postings = await _transactions.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
            var disbursements = 0;
            var splits = 0;

            foreach (var loan in loans)
            {
                // Principal/interest split on the register for repayments made before it existed.
                loan.BackfillRepaymentSplit();
                _loans.Update(loan);

                if (await _transactions.FindBySourceAsync(request.FarmId, loan.Id, TransactionCategory.LoanDisbursement, cancellationToken) is null)
                {
                    await _posting.PostAsync(new LedgerPosting(
                        loan.TenantId, loan.FarmId, TransactionCategory.LoanDisbursement,
                        loan.PrincipalAmountBdt, loan.DisbursementDate, loan.Id, "Finance",
                        $"Loan received from {loan.LenderName}",
                        Notes: "Posted by ledger repair (loan recorded before disbursements were booked)"), cancellationToken);
                    disbursements++;
                }

                if (loan.TotalInterestBdt <= 0)
                    continue;

                // Old repayments were booked whole as principal; move the interest part to expense.
                var interestShare = loan.TotalInterestBdt / loan.TotalPayableBdt;
                var legacy = postings.Where(t =>
                    t.Category == TransactionCategory.LoanRepayment
                    && t.SourceDocumentId is null
                    && t.Description.EndsWith(loan.LenderName, StringComparison.OrdinalIgnoreCase));

                foreach (var row in legacy)
                {
                    var interest = Math.Round(row.AmountBdt * interestShare, 2);
                    var principal = row.AmountBdt - interest;

                    row.LinkSource(row.Id, "Finance");
                    row.Revise(principal, row.TransactionDate, row.Description,
                        $"{row.Notes} (principal portion; interest split out by ledger repair)".Trim(), row.Settlement);
                    await _transactions.UpdateAsync(row, cancellationToken);

                    await _posting.PostAsync(new LedgerPosting(
                        row.TenantId, row.FarmId, TransactionCategory.InterestExpense,
                        interest, row.TransactionDate, row.Id, "Finance",
                        $"Interest on loan from {loan.LenderName}",
                        Notes: "Interest portion split out by ledger repair",
                        ReferenceId: row.ReferenceId), cancellationToken);
                    splits++;
                }
            }

            if (disbursements > 0)
                messages.Add($"Posted {disbursements} loan receipt(s) that were missing from the cash book.");
            if (splits > 0)
                messages.Add($"Split {splits} old loan repayment(s) into principal and interest.");

            return (disbursements, splits);
        }

        private async Task<(int Resynced, int Disposals)> RepairAnimalsAsync(
            Guid farmId, List<string> messages, CancellationToken cancellationToken)
        {
            var before = await _transactions.GetAllByFarmIdAsync(farmId, cancellationToken);
            var disposalKeys = before
                .Where(t => t.SourceDocumentId.HasValue
                            && t.Category is TransactionCategory.CostOfAnimalsSold or TransactionCategory.LivestockLoss)
                .Select(t => t.SourceDocumentId!.Value)
                .ToHashSet();

            var animals = await _animals.GetAllByFarmAsync(farmId, cancellationToken);
            var disposals = 0;

            foreach (var animal in animals)
            {
                // Purchase: correct amount and date, adopt legacy rows; then release cost if gone.
                await _livestock.SyncPurchaseAsync(animal, cancellationToken);

                if (!AccountingReconciliationService.OnFarm(animal) && !disposalKeys.Contains(animal.Id)
                    && AccountingReconciliationService.PurchaseCost(animal) > 0)
                {
                    disposals++;
                }
            }

            messages.Add($"Re-synchronised purchase, sale and disposal postings for {animals.Count} animal(s).");
            if (disposals > 0)
                messages.Add($"Released the purchase cost of {disposals} sold/dead animal(s) from the livestock asset.");

            return (animals.Count, disposals);
        }

        private async Task<decimal> AdjustInventoryAsync(
            RepairLedgerCommand request, List<string> messages, CancellationToken cancellationToken)
        {
            var postings = await _transactions.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
            var lines = await _reconciliation.ReconcileAsync(request.FarmId, postings, cancellationToken);
            var inventory = lines.First(l => l.Key == "inventory");

            // Books above the count: stock was used or lost without being recorded.
            // Books below the count: stock came in without a purchase entry (e.g. old opening stock).
            var difference = inventory.RegisterBdt - inventory.BooksBdt;
            if (Math.Abs(difference) <= AccountingReconciliationService.Tolerance)
                return 0m;

            var today = DateTime.UtcNow.Date;
            var gain = difference > 0;
            await _posting.PostAsync(new LedgerPosting(
                request.TenantId, request.FarmId,
                gain ? TransactionCategory.OwnerCapital : TransactionCategory.InventoryWriteOff,
                Math.Abs(difference), today, Guid.NewGuid(), InventoryAccounting.Module,
                gain ? "Stock count adjustment - unrecorded / opening stock" : "Stock count adjustment - unrecorded usage & shrinkage",
                Settlement: SettlementAccount.Inventory,
                Notes: $"Books {inventory.BooksBdt:N2} vs stock on hand {inventory.RegisterBdt:N2} BDT",
                ReferenceId: "STOCK-COUNT"), cancellationToken);

            messages.Add(gain
                ? $"Brought {difference:N2} BDT of stock on hand into the books as opening / owner capital."
                : $"Wrote off {-difference:N2} BDT of stock missing from the books as shrinkage.");
            return Math.Round(difference, 2);
        }
    }
}
