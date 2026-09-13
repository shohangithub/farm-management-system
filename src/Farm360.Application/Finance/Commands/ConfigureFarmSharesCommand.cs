using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record ConfigureFarmSharesCommand(
    Guid TenantId,
    Guid FarmId,
    int TotalShares,
    decimal SharePriceBdt,
    int OwnerShareCount,
    int MinimumPurchaseShares = 1,
    bool IsShareSaleOpen = true,
    string? ValuationNotes = null
) : IRequest<FarmShareConfigDto>;

public class ConfigureFarmSharesCommandValidator : AbstractValidator<ConfigureFarmSharesCommand>
{
    public ConfigureFarmSharesCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("FarmId is required.");
        RuleFor(x => x.TotalShares).GreaterThan(0).WithMessage("Total shares must be greater than zero.");
        RuleFor(x => x.SharePriceBdt).GreaterThan(0).WithMessage("Share price must be greater than zero.");
        RuleFor(x => x.OwnerShareCount).GreaterThanOrEqualTo(0).WithMessage("Owner shares cannot be negative.");
        RuleFor(x => x.MinimumPurchaseShares).GreaterThanOrEqualTo(1).WithMessage("Minimum purchase shares must be at least 1.");
    }
}

public class ConfigureFarmSharesCommandHandler : IRequestHandler<ConfigureFarmSharesCommand, FarmShareConfigDto>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfigureFarmSharesCommandHandler(
        IFarmShareRepository shareRepository,
        IUnitOfWork unitOfWork)
    {
        _shareRepository = shareRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<FarmShareConfigDto> Handle(ConfigureFarmSharesCommand request, CancellationToken cancellationToken)
    {
        var existingConfig = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken);

        FarmShareConfig config;
        if (existingConfig == null)
        {
            config = FarmShareConfig.Create(
                request.TenantId,
                request.FarmId,
                request.TotalShares,
                request.SharePriceBdt,
                request.OwnerShareCount,
                request.MinimumPurchaseShares,
                request.IsShareSaleOpen,
                request.ValuationNotes
            );

            _shareRepository.AddConfig(config);
        }
        else
        {
            existingConfig.UpdateConfiguration(
                request.TotalShares,
                request.OwnerShareCount,
                request.SharePriceBdt,
                request.MinimumPurchaseShares,
                request.IsShareSaleOpen,
                request.ValuationNotes
            );

            config = existingConfig;
            _shareRepository.UpdateConfig(config);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new FarmShareConfigDto(
            config.Id,
            config.FarmId,
            config.TotalShares,
            config.SharePriceBdt,
            config.OwnerShareCount,
            config.AllocatedShareCount,
            config.AvailableShareCount,
            config.TotalValuationBdt,
            config.AvailableValuationBdt,
            config.OwnerEquityValueBdt,
            config.OwnerOwnershipPercentage,
            config.MinimumPurchaseShares,
            config.IsShareSaleOpen,
            config.LastValuationDate,
            config.ValuationNotes
        );
    }
}
