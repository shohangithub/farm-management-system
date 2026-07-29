---
name: pagination_pattern
description: Reusable patterns for implementing server-side pagination, searching, and sorting in the Farm360 Angular + .NET application stack.
---

# Farm360 Pagination Pattern

Whenever you need to implement or upgrade a list view with server-side pagination, search, and sorting in this project, follow this standard pattern:

## 1. Backend: Entity Framework & Repository Layer
Implement `GetPagedAsync` on the repository using EF Core:
```csharp
public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
    int pageNumber, int pageSize, string? search, string? sortBy, bool sortDesc, CancellationToken cancellationToken)
{
    var query = _dbSet.AsQueryable();
    
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(x => x.Name.Contains(search));
    }
    
    var count = await query.CountAsync(cancellationToken);
    
    query = sortBy?.ToLowerInvariant() switch {
        "name" => sortDesc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
        _ => sortDesc ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.CreatedAtUtc)
    };
    
    var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return (items, count);
}
```
**CRITICAL**: Use `.ToLowerInvariant()` in switch statements for sorting keys, but NEVER use `.ToLower()` inside the EF Core LINQ query expressions due to `CA1304` analyzer warnings. EF Core automatically translates `.Contains(search)` in a case-insensitive manner for SQL Server by default.

## 2. Backend: MediatR Query & Endpoint
The query should inherit `IRequest<PagedResult<Dto>>` and construct the standard `PagedResult` object:
```csharp
public sealed record GetItemsQuery(int PageNumber, int PageSize, string? Search) : IRequest<PagedResult<Dto>>;

// In Handler:
var (items, count) = await _repository.GetPagedAsync(...);
var dtos = items.Select(x => x.ToDto()).ToList();
return new PagedResult<Dto>(dtos, count, pageNumber, pageSize);
```
Map endpoints using `[FromQuery]` attributes.

## 3. Frontend: Service and Params
Define a params interface and update the HTTP service method to handle `HttpParams`:
```typescript
export interface PagedResult<T> { items: T[]; totalCount: number; pageNumber: number; pageSize: number; hasNextPage: boolean; hasPreviousPage: boolean; }
export interface ItemsParams { pageNumber?: number; pageSize?: number; search?: string; }

// getItems(params?: ItemsParams): Observable<PagedResult<Dto>>
```

## 4. Frontend: Declarative Signals UI Component
Implement the component strictly with `ChangeDetectionStrategy.OnPush` using Signals and RxJS interoperability. NEVER use `ChangeDetectorRef`.

```typescript
readonly params = signal<ItemsParams>({ pageNumber: 1, pageSize: 20 });
readonly searchTerm = signal('');
private readonly refreshTrigger = signal(0);

private combinedParams = computed(() => ({ params: this.params(), refresh: this.refreshTrigger() }));

readonly result = toSignal(
  toObservable(this.combinedParams).pipe(
    tap(() => this.loading.set(true)),
    switchMap(({ params }) => this.svc.getItems(params)),
    tap(() => this.loading.set(false))
  )
);
```

**Key Interactions**:
- **Search**: `toObservable(this.searchTerm).pipe(debounceTime(350), distinctUntilChanged()).subscribe(t => this.params.update(p => ({...p, search: t, pageNumber: 1})))`
- **Pagination**: Use `pageNumber: (p.pageNumber || 1) + 1` based on `res.hasNextPage`.
- **URL Sync**: Keep URL query parameters in sync with `params()` via `this.router.navigate([], { queryParams: ... })`.
