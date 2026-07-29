using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Models;

namespace Farm360.Application.Livestock.Queries;

public sealed record GetBreedListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    string? Category = null,
    string? MainPurpose = null,
    string? SortBy = null,
    bool SortDesc = false) : IRequest<PagedResult<BreedDto>>;

public sealed class GetBreedListQueryHandler(IBreedRepository repository) : IRequestHandler<GetBreedListQuery, PagedResult<BreedDto>>
{
    public async Task<PagedResult<BreedDto>> Handle(GetBreedListQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.Search,
            request.Category,
            request.MainPurpose,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(b => b.ToDto()).ToList();
        return new PagedResult<BreedDto>(dtos, count, pageNumber, pageSize);
    }
}

public sealed record GetBreedByIdQuery(Guid Id) : IRequest<BreedDto?>;

public sealed class GetBreedByIdQueryHandler(IBreedRepository repository) : IRequestHandler<GetBreedByIdQuery, BreedDto?>
{
    public async Task<BreedDto?> Handle(GetBreedByIdQuery request, CancellationToken cancellationToken)
    {
        var breed = await repository.GetByIdAsync(request.Id, cancellationToken);
        return breed?.ToDto();
    }
}
