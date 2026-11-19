using Farm360.Application.Common.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Services;

/// <inheritdoc cref="IFarmAccessService"/>
/// <remarks>
/// Queries go through the tenant-filtered sets, so a farm/animal/batch of another tenant is simply
/// "not found" and therefore not accessible. Results are memoised for the request (scoped).
/// </remarks>
public sealed class FarmAccessService(
    ApplicationDbContext context,
    ICurrentUserService currentUser) : IFarmAccessService
{
    private readonly Dictionary<Guid, bool> _farmCache = [];

    private bool IsUnrestricted => !currentUser.IsAuthenticated || currentUser.IsSystemUser;

    public async Task<bool> CanAccessFarmAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted || farmId == Guid.Empty)
        {
            return true;
        }

        if (_farmCache.TryGetValue(farmId, out var cached))
        {
            return cached;
        }

        var allowed = currentUser.AssignedFarmIds;
        var result = (allowed is null || allowed.Contains(farmId))
                     && await context.Farms.AsNoTracking().AnyAsync(f => f.Id == farmId, cancellationToken);

        _farmCache[farmId] = result;
        return result;
    }

    public async Task<bool> CanAccessAnimalAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted || animalId == Guid.Empty)
        {
            return true;
        }

        var farmId = await context.Animals.AsNoTracking()
            .Where(a => a.Id == animalId)
            .Select(a => (Guid?)a.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        return farmId.HasValue && await CanAccessFarmAsync(farmId.Value, cancellationToken);
    }

    public async Task<bool> CanAccessBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted || batchId == Guid.Empty)
        {
            return true;
        }

        var farmId = await context.AnimalBatches.AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => (Guid?)b.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        return farmId.HasValue && await CanAccessFarmAsync(farmId.Value, cancellationToken);
    }
}
