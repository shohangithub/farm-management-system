using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.MedicalTreatments;

public sealed record GetTreatmentListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    Guid? AnimalId = null,
    TreatmentStatus? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false
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
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.AnimalId,
            request.Status,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(t => t.ToDto()).ToList();
        return new PagedResult<MedicalTreatmentDto>(dtos, count, pageNumber, pageSize);
    }
}
