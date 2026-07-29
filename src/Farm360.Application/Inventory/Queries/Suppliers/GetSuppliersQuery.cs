using Farm360.Application.Common.Models;
using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.Mappings;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.Suppliers;

public sealed record GetSuppliersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false) : IRequest<PagedResult<SupplierDto>>;

public sealed class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, PagedResult<SupplierDto>>
{
    private readonly ISupplierRepository _repository;

    public GetSuppliersQueryHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (suppliers, count) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = suppliers.Select(x => x.ToDto()).ToList();
        return new PagedResult<SupplierDto>(dtos, count, pageNumber, pageSize);
    }
}
