using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Queries;

public sealed record GetBatchesQuery(
    Guid FarmId,
    BatchStatus? Status,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedBatchListDto>;

public sealed class GetBatchesQueryHandler : IRequestHandler<GetBatchesQuery, PagedBatchListDto>
{
    private readonly IAnimalBatchRepository _repository;

    public GetBatchesQueryHandler(IAnimalBatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedBatchListDto> Handle(GetBatchesQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.Status,
            cancellationToken);

        var dtos = items.Select(b => new BatchDto(
            b.Id,
            b.TenantId,
            b.FarmId,
            b.Name,
            b.Status,
            b.Notes,
            b.Animals.Count,
            b.CreatedAtUtc)).ToList().AsReadOnly();

        return new PagedBatchListDto(dtos, totalCount, pageNumber, pageSize);
    }
}
