using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Farms.Commands;

/// <summary>
/// Sets only the farm's latitude/longitude (the Weather page's "Set location"). Unlike
/// <see cref="UpdateFarmCommand"/> it leaves every other farm field as it is. The public
/// <see cref="FarmId"/> puts it under the farm access check (FarmAccessBehavior).
/// </summary>
public sealed record SetFarmLocationCommand(Guid FarmId, double Latitude, double Longitude) : IRequest, ITransactionalCommand;

/// <summary>Same ranges as the farm form (<see cref="UpdateFarmCommandValidator"/>).</summary>
public sealed class SetFarmLocationCommandValidator : AbstractValidator<SetFarmLocationCommand>
{
    public SetFarmLocationCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public sealed class SetFarmLocationCommandHandler(
    IFarmRepository repository,
    ITenantService tenantService,
    IUnitOfWork unitOfWork) : IRequestHandler<SetFarmLocationCommand>
{
    public async Task Handle(SetFarmLocationCommand request, CancellationToken cancellationToken)
    {
        var farm = await repository.GetByIdAsync(tenantService.TenantId, request.FarmId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Farm), request.FarmId);

        farm.SetLocation(request.Latitude, request.Longitude);

        repository.Update(farm);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
