using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record TransferSharesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid FromInvestorId,
    Guid ToInvestorId,
    int ShareCount,
    decimal? PricePerShareBdt = null,
    DateTime? TransactionDate = null,
    string? ReferenceId = null,
    string? Notes = null
) : IRequest<bool>;

public class TransferSharesCommandValidator : AbstractValidator<TransferSharesCommand>
{
    public TransferSharesCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("FarmId is required.");
        RuleFor(x => x.FromInvestorId).NotEmpty().WithMessage("Source investor is required.");
        RuleFor(x => x.ToInvestorId).NotEmpty().WithMessage("Target investor is required.");
        RuleFor(x => x.FromInvestorId).NotEqual(x => x.ToInvestorId).WithMessage("Cannot transfer shares to the same investor.");
        RuleFor(x => x.ShareCount).GreaterThan(0).WithMessage("Share count must be greater than zero.");
    }
}

public class TransferSharesCommandHandler : IRequestHandler<TransferSharesCommand, bool>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IInvestorRepository _investorRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TransferSharesCommandHandler(
        IFarmShareRepository shareRepository,
        IInvestorRepository investorRepository,
        IUnitOfWork unitOfWork)
    {
        _shareRepository = shareRepository;
        _investorRepository = investorRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(TransferSharesCommand request, CancellationToken cancellationToken)
    {
        var config = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken)
            ?? throw new InvalidOperationException("Shares are not configured for this farm.");

        var fromHolding = await _shareRepository.GetHoldingByInvestorAndFarmAsync(request.FromInvestorId, request.FarmId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShareHolding), request.FromInvestorId);

        if (request.ShareCount > fromHolding.ShareCount)
            throw new InvalidOperationException($"Cannot transfer {request.ShareCount} shares. Source investor holds {fromHolding.ShareCount} shares.");

        var fromInvestor = await _investorRepository.GetByIdAsync(request.FromInvestorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.FromInvestorId);

        var toInvestor = await _investorRepository.GetByIdAsync(request.ToInvestorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.ToInvestorId);

        var effectivePrice = request.PricePerShareBdt ?? config.SharePriceBdt;
        var txDate = request.TransactionDate ?? DateTime.UtcNow;

        // 1. Deduct from sender
        fromHolding.RemoveShares(request.ShareCount);
        _shareRepository.UpdateHolding(fromHolding);

        // 2. Add to receiver
        var toHolding = await _shareRepository.GetHoldingByInvestorAndFarmAsync(request.ToInvestorId, request.FarmId, cancellationToken);
        if (toHolding == null)
        {
            toHolding = ShareHolding.Create(
                request.TenantId,
                request.ToInvestorId,
                request.FarmId,
                request.ShareCount,
                effectivePrice,
                notes: request.Notes
            );
            _shareRepository.AddHolding(toHolding);
        }
        else
        {
            toHolding.AddShares(request.ShareCount, effectivePrice);
            _shareRepository.UpdateHolding(toHolding);
        }

        // 3. Record transfer audit records
        var fromTx = ShareTransaction.Create(
            request.TenantId,
            request.FarmId,
            request.FromInvestorId,
            ShareTransactionType.Transfer,
            request.ShareCount,
            effectivePrice,
            txDate,
            fromHolding.Id,
            counterpartyInvestorId: request.ToInvestorId,
            referenceId: request.ReferenceId,
            notes: $"Transferred {request.ShareCount} shares to {toInvestor.Name}. {request.Notes}".Trim()
        );
        _shareRepository.AddTransaction(fromTx);

        var toTx = ShareTransaction.Create(
            request.TenantId,
            request.FarmId,
            request.ToInvestorId,
            ShareTransactionType.Transfer,
            request.ShareCount,
            effectivePrice,
            txDate,
            toHolding.Id,
            counterpartyInvestorId: request.FromInvestorId,
            referenceId: request.ReferenceId,
            notes: $"Received {request.ShareCount} shares from {fromInvestor.Name}. {request.Notes}".Trim()
        );
        _shareRepository.AddTransaction(toTx);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
