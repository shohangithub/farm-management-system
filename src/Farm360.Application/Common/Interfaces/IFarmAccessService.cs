namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Central per-farm authorization: a farm (or an animal/batch, through its farm) is accessible when
/// it belongs to the caller's tenant AND is in the caller's allowed farms
/// (<see cref="ICurrentUserService.AssignedFarmIds"/>; null = every farm in the tenant).
/// Background work (no authenticated user) and platform administrators are not restricted.
/// </summary>
public interface IFarmAccessService
{
    Task<bool> CanAccessFarmAsync(Guid farmId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessAnimalAsync(Guid animalId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}
