namespace Farm360.Shared.Primitives;

/// <summary>
/// Standardized paged query parameters.
/// Constitution §6 (API Standards): All list endpoints accept these query parameters.
/// Defaults: PageNumber=1, PageSize=25. Max PageSize enforced at 100.
/// </summary>
public sealed record PagedRequest
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MinPageNumber = 1;

    private int _pageNumber = MinPageNumber;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < MinPageNumber ? MinPageNumber : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? DefaultPageSize : value;
    }

    /// <summary>Optional: sort field name (validated per-query).</summary>
    public string? SortBy { get; init; }

    /// <summary>Optional: ascending (default) or descending.</summary>
    public bool IsDescending { get; init; } = false;

    /// <summary>Optional: full-text search term.</summary>
    public string? Search { get; init; }

    /// <summary>LINQ Skip value derived from page number and size.</summary>
    public int Skip => (PageNumber - 1) * PageSize;
}
