using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using MediatR;

namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>Sell-or-keep advice for one animal on the farm, with its day-by-day projection.</summary>
public sealed record GetAnimalSellOrKeepQuery(Guid AnimalId) : IRequest<SellOrKeepAdvice?>;

public sealed class GetAnimalSellOrKeepQueryHandler(IAnimalSellOrKeepAdvisor advisor, IFarmAccessService farmAccess)
    : IRequestHandler<GetAnimalSellOrKeepQuery, SellOrKeepAdvice?>
{
    public async Task<SellOrKeepAdvice?> Handle(GetAnimalSellOrKeepQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await farmAccess.CanAccessAnimalAsync(request.AnimalId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenAccessException("You do not have access to this animal.");

        return await advisor.AdviseAnimalAsync(request.AnimalId, null, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Sell-or-keep advice for every animal on a farm (optionally one batch), without projections.</summary>
/// <remarks>The <see cref="FarmId"/> property puts the request through the farm-access check.</remarks>
public sealed record GetFarmSellOrKeepQuery(Guid FarmId, Guid? BatchId = null) : IRequest<IReadOnlyList<SellOrKeepAdvice>>;

public sealed class GetFarmSellOrKeepQueryHandler(IAnimalSellOrKeepAdvisor advisor)
    : IRequestHandler<GetFarmSellOrKeepQuery, IReadOnlyList<SellOrKeepAdvice>>
{
    public Task<IReadOnlyList<SellOrKeepAdvice>> Handle(GetFarmSellOrKeepQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return advisor.AdviseFarmAsync(request.FarmId, null, request.BatchId, null, includeProjection: false, cancellationToken);
    }
}
