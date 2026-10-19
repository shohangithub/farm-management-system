using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Inventory;

public sealed class MeasureUnitRepository(ApplicationDbContext db) : IMeasureUnitRepository
{
    public async Task<IReadOnlyList<MeasureUnit>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.MeasureUnits.Include(u => u.Conversions).OrderBy(u => u.Name).ToListAsync(cancellationToken);

    public Task<MeasureUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.MeasureUnits.Include(u => u.Conversions).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<MeasureUnit?> GetByConversionIdAsync(Guid conversionId, CancellationToken cancellationToken = default) =>
        db.MeasureUnits.Include(u => u.Conversions).FirstOrDefaultAsync(u => u.Conversions.Any(c => c.Id == conversionId), cancellationToken);

    public async Task AddAsync(MeasureUnit unit, CancellationToken cancellationToken = default) =>
        await db.MeasureUnits.AddAsync(unit, cancellationToken);

    public void Update(MeasureUnit unit) => db.MeasureUnits.Update(unit);

    public void Remove(MeasureUnit unit)
    {
        foreach (var c in unit.Conversions)
            db.UnitConversions.Remove(c);
        db.MeasureUnits.Remove(unit);
    }

    public Task<UnitConversion?> GetConversionAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.UnitConversions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void RemoveConversion(UnitConversion conversion) => db.UnitConversions.Remove(conversion);

    public async Task<IReadOnlyList<string>> ItemsUsingAsync(IReadOnlyCollection<Guid> baseUnitIds, IReadOnlyCollection<Guid> conversionIds, CancellationToken cancellationToken = default)
    {
        return await db.InventoryItems
            .Where(i => (i.BaseUnitId != null && baseUnitIds.Contains(i.BaseUnitId.Value))
                     || (i.DefaultUnitId != null && conversionIds.Contains(i.DefaultUnitId.Value)))
            .Select(i => i.Name)
            .Take(10)
            .ToListAsync(cancellationToken);
    }
}
