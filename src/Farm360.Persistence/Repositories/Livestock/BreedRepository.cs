using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Livestock;

public sealed class BreedRepository(ApplicationDbContext context) : IBreedRepository
{
    private readonly DbSet<Breed> _dbSet = context.Set<Breed>();

    public async Task<Breed?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Breed>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Breed> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        string? category = null,
        string? mainPurpose = null,
        string? sortBy = null,
        bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search) ||
                                     (x.Description != null && x.Description.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(mainPurpose))
        {
            query = query.Where(x => x.MainPurpose == mainPurpose);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy?.ToLowerInvariant() switch
        {
            "name" => sortDesc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "category" => sortDesc ? query.OrderByDescending(x => x.Category) : query.OrderBy(x => x.Category),
            "mainpurpose" => sortDesc ? query.OrderByDescending(x => x.MainPurpose) : query.OrderBy(x => x.MainPurpose),
            "standardadg" => sortDesc ? query.OrderByDescending(x => x.StandardAdgMax) : query.OrderBy(x => x.StandardAdgMax),
            "fcr" => sortDesc ? query.OrderByDescending(x => x.FcrMin) : query.OrderBy(x => x.FcrMin),
            _ => sortDesc ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.Name)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Breed?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
#pragma warning disable CA1304, CA1311, CA1862
        var lowerName = name.ToLower();
        return await _dbSet.FirstOrDefaultAsync(b => b.Name.ToLower() == lowerName, cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862
    }

    public void Add(Breed breed)
    {
        _dbSet.Add(breed);
    }

    public void Update(Breed breed)
    {
        _dbSet.Update(breed);
    }

    public void Delete(Breed breed)
    {
        _dbSet.Remove(breed);
    }
}
