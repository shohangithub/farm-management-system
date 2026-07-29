using Farm360.Domain.Livestock;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Livestock.Repositories;

public interface IBreedRepository
{
    Task<Breed?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Breed>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Breed?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Breed> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        string? category = null,
        string? mainPurpose = null,
        string? sortBy = null,
        bool sortDesc = false,
        CancellationToken cancellationToken = default);
    
    void Add(Breed breed);
    void Update(Breed breed);
    void Delete(Breed breed);
}
