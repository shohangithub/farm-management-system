using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.MedicalTreatments;

public sealed record GetTreatmentListQuery(
    Guid? AnimalId = null,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PagedResult<MedicalTreatmentDto>>;

internal sealed class GetTreatmentListQueryHandler : IRequestHandler<GetTreatmentListQuery, PagedResult<MedicalTreatmentDto>>
{
    private readonly IMedicalTreatmentRepository _repository;

    public GetTreatmentListQueryHandler(IMedicalTreatmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<MedicalTreatmentDto>> Handle(GetTreatmentListQuery request, CancellationToken cancellationToken)
    {
        var (items, count) = await _repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.AnimalId,
            cancellationToken);

        var dtos = items.Select(t => t.ToDto()).ToList();
        return new PagedResult<MedicalTreatmentDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
