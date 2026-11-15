using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Contracts.Intelligence;
using MediatR;

namespace Farm360.Application.Intelligence.Queries.GetProjectionDefaults;

public sealed record GetProjectionDefaultsQuery(Guid AnimalId) : IRequest<ProjectionDefaultsDto>;

public sealed class GetProjectionDefaultsQueryHandler : IRequestHandler<GetProjectionDefaultsQuery, ProjectionDefaultsDto>
{
    private readonly IProjectionDefaultsResolver _resolver;

    public GetProjectionDefaultsQueryHandler(IProjectionDefaultsResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<ProjectionDefaultsDto> Handle(GetProjectionDefaultsQuery request, CancellationToken cancellationToken)
    {
        var defaults = await _resolver.ResolveDefaultsAsync(request.AnimalId, cancellationToken);

        // The projection starts at the animal's latest weight, i.e. today. Day 0's investment is
        // therefore everything already spent on it, not just the price paid at purchase — using the
        // bare purchase price would leave out all feed, vet and labour to date and overstate profit.
        if (defaults.CostToDateBdt is { Value: > 0 } costToDate
            && costToDate.Value > defaults.PurchasePriceBdt.Value)
        {
            defaults = defaults with { PurchasePriceBdt = costToDate };
        }

        return defaults;
    }
}
