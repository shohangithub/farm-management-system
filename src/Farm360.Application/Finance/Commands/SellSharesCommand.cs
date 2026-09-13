using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record SellSharesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid InvestorId,
    int ShareCount,
    decimal? PricePerShareBdt = null,
    DateTime? TransactionDate = null,
    string? ReferenceId = null,
    string? Notes = null
) : IRequest<ShareHoldingDto>;

public class SellSharesCommandValidator : AbstractValidator<SellSharesCommand>
{
    public SellSharesCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("FarmId is required.");
        RuleFor(x => x.InvestorId).NotEmpty().WithMessage("InvestorId is required.");
        RuleFor(x => x.ShareCount).GreaterThan(0).WithMessage("Share count must be greater than zero.");
        When(x => x.PricePerShareBdt.HasValue, () =>
        {
            RuleFor(x => x.PricePerShareBdt!.Value).GreaterThan(0).WithMessage("Price per share must be positive.");
        });
    }
}

public class SellSharesCommandHandler : IRequestHandler<SellSharesCommand, ShareHoldingDto>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IInvestorRepository _investorRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SellSharesCommandHandler(
        IFarmShareRepository shareRepository,
        IInvestorRepository investorRepository,
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _shareRepository = shareRepository;
        _investorRepository = investorRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShareHoldingDto> Handle(SellSharesCommand request, CancellationToken cancellationToken)
    {
        var config = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken)
            ?? throw new InvalidOperationException("Shares are not configured for this farm.");

        var holding = await _shareRepository.GetHoldingByInvestorAndFarmAsync(request.InvestorId, request.FarmId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShareHolding), request.InvestorId);

        if (request.ShareCount > holding.ShareCount)
            throw new InvalidOperationException($"Cannot sell {request.ShareCount} shares. Investor currently holds {holding.ShareCount} shares.");

        var investor = await _investorRepository.GetByIdWithTransactionsAsync(request.InvestorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.InvestorId);

        var effectivePrice = request.PricePerShareBdt ?? config.SharePriceBdt;
        var totalPayout = Math.Round(request.ShareCount * effectivePrice, 2);
        var txDate = request.TransactionDate ?? DateTime.UtcNow;

        // 1. Remove shares from holding
        holding.RemoveShares(request.ShareCount);
        _shareRepository.UpdateHolding(holding);

        // 2. Return shares to farm available pool
        config.DeallocateShares(request.ShareCount);
        _shareRepository.UpdateConfig(config);

        // 3. Post to General Ledger as Investor Capital Withdrawal Expense
        var glTx = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            TransactionType.Expense,
            TransactionCategory.InvestorWithdrawal,
            totalPayout,
            txDate,
            request.ReferenceId ?? string.Empty,
            request.Notes ?? string.Empty,
            $"Share redemption ({request.ShareCount} shares @ {effectivePrice:N2} BDT) for {investor.Name}",
            isAutomated: true,
            sourceModule: "ShareMarket"
        );
        await _transactionRepository.AddAsync(glTx, cancellationToken);

        // 4. Record on Investor entity
        var withdrawalCapital = Math.Min(investor.CurrentCapitalBdt, totalPayout);
        if (withdrawalCapital > 0)
        {
            investor.RecordTransaction(
                request.TenantId,
                InvestorTransactionType.CapitalWithdrawal,
                withdrawalCapital,
                txDate,
                request.ReferenceId,
                $"Sold {request.ShareCount} farm equity shares @ {effectivePrice:N2} BDT",
                glTx.Id
            );
            _investorRepository.Update(investor);
        }

        // 5. Create ShareTransaction audit record
        var shareTx = ShareTransaction.Create(
            request.TenantId,
            request.FarmId,
            request.InvestorId,
            ShareTransactionType.Sale,
            request.ShareCount,
            effectivePrice,
            txDate,
            holding.Id,
            referenceId: request.ReferenceId,
            notes: request.Notes,
            financialTransactionId: glTx.Id
        );
        _shareRepository.AddTransaction(shareTx);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var currentValue = holding.ShareCount * config.SharePriceBdt;
        var gainLoss = currentValue - holding.TotalInvestedBdt;
        var roi = holding.TotalInvestedBdt > 0
            ? Math.Round((gainLoss / holding.TotalInvestedBdt) * 100m, 2)
            : 0m;
        var ownershipPct = config.TotalShares > 0
            ? Math.Round(((decimal)holding.ShareCount / config.TotalShares) * 100m, 2)
            : 0m;

        return new ShareHoldingDto(
            holding.Id,
            investor.Id,
            investor.Name,
            investor.Phone,
            investor.Email,
            holding.FarmId,
            holding.ShareCount,
            holding.AveragePurchasePriceBdt,
            holding.TotalInvestedBdt,
            currentValue,
            ownershipPct,
            gainLoss,
            roi,
            holding.CertificateNumber,
            holding.Notes,
            holding.IsActive
        );
    }
}
