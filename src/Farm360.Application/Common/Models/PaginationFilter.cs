namespace Farm360.Application.Common.Models;

public record PaginationFilter
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    public int PageNumber { get; init; } = 1;

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = (value > MaxPageSize) ? MaxPageSize : (value < 1 ? 1 : value);
    }

    public string? SearchTerm { get; init; }
    
    // Status filter: null means All, 1 means Active, 2 means Inactive
    public int? Status { get; init; }
}
