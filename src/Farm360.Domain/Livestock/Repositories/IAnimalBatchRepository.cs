using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock.Repositories;

public interface IAnimalBatchRepository
{
    Task<AnimalBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AnimalBatch batch, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<AnimalBatch> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid farmId,
        BatchStatus? status,
        CancellationToken cancellationToken = default);
}
