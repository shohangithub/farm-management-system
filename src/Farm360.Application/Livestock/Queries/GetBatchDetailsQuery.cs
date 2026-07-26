using Farm360.Application.Common.Exceptions;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Queries;

public sealed record GetBatchDetailsQuery(Guid BatchId) : IRequest<BatchDto?>;

public sealed class GetBatchDetailsQueryHandler : IRequestHandler<GetBatchDetailsQuery, BatchDto?>
{
    private readonly IAnimalBatchRepository _repository;

    public GetBatchDetailsQueryHandler(IAnimalBatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BatchDto?> Handle(GetBatchDetailsQuery request, CancellationToken cancellationToken)
    {
        var batch = await _repository.GetByIdAsync(request.BatchId, cancellationToken);
        if (batch is null) return null;

        return new BatchDto(
            batch.Id,
            batch.TenantId,
            batch.FarmId,
            batch.Name,
            batch.Status,
            batch.Notes,
            batch.Animals.Count,
            batch.CreatedAtUtc);
    }
}
