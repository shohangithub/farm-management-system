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

public record PurchaseSharesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid InvestorId,
    int ShareCount,
    decimal? PricePerShareBdt = null,
    DateTime? TransactionDate = null,
    string? ReferenceId = null,
    string? Notes = null
) : IRequest<ShareHoldingDto>;

public class PurchaseSharesCommandValidator : AbstractValidator<PurchaseSharesCommand>
{
    public PurchaseSharesCommandValidator()
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

public class PurchaseSharesCommandHandler : IRequestHandler<PurchaseSharesCommand, ShareHoldingDto>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IInvestorRepository _investorRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PurchaseSharesCommandHandler(
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

    public async Task<ShareHoldingDto> Handle(PurchaseSharesCommand request, CancellationToken cancellationToken)
    {
        var config = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken)
            ?? throw new InvalidOperationException("Shares are not configured for this farm. Configure shares first.");

        if (!config.IsShareSaleOpen)
            throw new InvalidOperationException("Share purchasing is currently closed for this farm.");

        if (request.ShareCount < config.MinimumPurchaseShares)
            throw new ArgumentException($"Minimum purchase quantity is {config.MinimumPurchaseShares} shares.", nameof(request));

        if (request.ShareCount > config.AvailableShareCount)
            throw new InvalidOperationException($"Requested {request.ShareCount} shares, but only {config.AvailableShareCount} shares are available.");

        var investor = await _investorRepository.GetByIdWithTransactionsAsync(request.InvestorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.InvestorId);

        if (investor.FarmId != request.FarmId)
            throw new ArgumentException("Investor does not belong to the specified farm.", nameof(request));

        var effectivePrice = request.PricePerShareBdt ?? config.SharePriceBdt;
        var totalAmount = Math.Round(request.ShareCount * effectivePrice, 2);
        var txDate = request.TransactionDate ?? DateTime.UtcNow;

        // 1. Update or create ShareHolding
        var holding = await _shareRepository.GetHoldingByInvestorAndFarmAsync(request.InvestorId, request.FarmId, cancellationToken);
        if (holding == null)
        {
            holding = ShareHolding.Create(
                request.TenantId,
                request.InvestorId,
                request.FarmId,
                request.ShareCount,
                effectivePrice,
                notes: request.Notes
            );
            _shareRepository.AddHolding(holding);
        }
        else
        {
            holding.AddShares(request.ShareCount, effectivePrice);
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                holding.UpdateNotes(request.Notes);
            }
            _shareRepository.UpdateHolding(holding);
        }

        // 2. Allocate shares from farm config pool
        config.AllocateShares(request.ShareCount);
        _shareRepository.UpdateConfig(config);

        // 3. Post to General Ledger as Investor Capital Income
        var glTx = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            TransactionType.Income,
            TransactionCategory.InvestorCapital,
            totalAmount,
            txDate,
            request.ReferenceId ?? string.Empty,
            request.Notes ?? string.Empty,
            $"Share purchase ({request.ShareCount} shares @ {effectivePrice:N2} BDT) by {investor.Name}",
            isAutomated: true,
            sourceModule: "ShareMarket"
        );
        await _transactionRepository.AddAsync(glTx, cancellationToken);

        // 4. Record on Investor aggregate root
        investor.RecordTransaction(
            request.TenantId,
            InvestorTransactionType.CapitalContribution,
            totalAmount,
            txDate,
            request.ReferenceId,
            $"Bought {request.ShareCount} farm equity shares @ {effectivePrice:N2} BDT",
            glTx.Id
        );
        _investorRepository.Update(investor);

        // 5. Create ShareTransaction audit record
        var shareTx = ShareTransaction.Create(
            request.TenantId,
            request.FarmId,
            request.InvestorId,
            ShareTransactionType.Purchase,
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

        // Calculate Dto metrics
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
