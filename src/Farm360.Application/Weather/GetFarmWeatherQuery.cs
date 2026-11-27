using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using MediatR;

namespace Farm360.Application.Weather;

/// <summary>
/// The farm's weather for the dashboard card, the Weather page and the Today's-tasks banner. Farm access
/// is checked by the FarmAccess pipeline behaviour (the request carries <see cref="FarmId"/>).
/// A farm without coordinates answers <c>NeedsLocation = true</c>, not an error.
/// </summary>
public sealed record GetFarmWeatherQuery(Guid FarmId) : IRequest<FarmWeatherDto>;

internal sealed class GetFarmWeatherQueryHandler(IFarmWeatherService weather, ITenantService tenantService)
    : IRequestHandler<GetFarmWeatherQuery, FarmWeatherDto>
{
    public async Task<FarmWeatherDto> Handle(GetFarmWeatherQuery request, CancellationToken cancellationToken) =>
        await weather.GetAsync(tenantService.TenantId, request.FarmId, cancellationToken)
        ?? throw new NotFoundException("Farm", request.FarmId);
}
