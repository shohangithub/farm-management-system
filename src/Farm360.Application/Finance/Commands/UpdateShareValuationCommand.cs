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

public record UpdateShareValuationCommand(
    Guid TenantId,
    Guid FarmId,
    decimal NewSharePriceBdt,
    string? ValuationNotes = null
) : IRequest<FarmShareConfigDto>;

public class UpdateShareValuationCommandValidator : AbstractValidator<UpdateShareValuationCommand>
{
    public UpdateShareValuationCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty().WithMessage("FarmId is required.");
        RuleFor(x => x.NewSharePriceBdt).GreaterThan(0).WithMessage("Share price must be greater than zero.");
    }
}

public class UpdateShareValuationCommandHandler : IRequestHandler<UpdateShareValuationCommand, FarmShareConfigDto>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateShareValuationCommandHandler(
        IFarmShareRepository shareRepository,
        IUnitOfWork unitOfWork)
    {
        _shareRepository = shareRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<FarmShareConfigDto> Handle(UpdateShareValuationCommand request, CancellationToken cancellationToken)
    {
        var config = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken)
            ?? throw new NotFoundException(nameof(FarmShareConfig), request.FarmId);

        var oldPrice = config.SharePriceBdt;
        config.UpdateValuation(request.NewSharePriceBdt, request.ValuationNotes);
        _shareRepository.UpdateConfig(config);

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
