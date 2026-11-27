namespace Farm360.Application.Livestock.Queries;

/// <summary>
/// Set-based lookup of shed and pen display names, so list queries can show "Shed B · B1"
/// without one call per shed. Implemented in Persistence (one query per table).
/// </summary>
public interface IAnimalLocationNames
{
    /// <summary>Shed id → label (shed name, else shed number) for the given ids. Unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetShedNamesAsync(IReadOnlyCollection<Guid> shedIds, CancellationToken cancellationToken = default);

    /// <summary>Pen id → label (pen number, else pen name) for the given ids. Unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetPenNamesAsync(IReadOnlyCollection<Guid> penIds, CancellationToken cancellationToken = default);
}
