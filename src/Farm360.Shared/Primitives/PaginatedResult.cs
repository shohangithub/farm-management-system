namespace Farm360.Shared.Primitives;

/// <summary>
/// Paginated result wrapper for list queries.
/// Constitution §7 (DTO Standards): All list responses are paginated.
/// </summary>
public sealed class PaginatedResult<T>
{
    private PaginatedResult(
        IReadOnlyList<T> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int FirstItemIndex => (PageNumber - 1) * PageSize + 1;
    public int LastItemIndex => Math.Min(PageNumber * PageSize, TotalCount);

    public static PaginatedResult<T> Create(
        IReadOnlyList<T> items,
        int totalCount,
        int pageNumber,
        int pageSize) => new(items, totalCount, pageNumber, pageSize);

    public static PaginatedResult<T> Empty(int pageNumber = 1, int pageSize = 25) =>
        new([], 0, pageNumber, pageSize);

    /// <summary>Maps items to a different type while preserving pagination metadata.</summary>
    public PaginatedResult<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        PaginatedResult<TResult>.Create(
            Items.Select(mapper).ToList().AsReadOnly(),
            TotalCount,
            PageNumber,
            PageSize);
}
