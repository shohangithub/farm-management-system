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
    private readonly Farm360.Domain.Livestock.Repositories.IAnimalRepository _animalRepository;

    public GetTreatmentListQueryHandler(IMedicalTreatmentRepository repository, Farm360.Domain.Livestock.Repositories.IAnimalRepository animalRepository)
    {
        _repository = repository;
        _animalRepository = animalRepository;
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

        var animalIds = items.Select(t => t.AnimalId).Distinct().ToList();
        var animals = animalIds.Count != 0 ? await _animalRepository.GetByIdsAsync(animalIds, cancellationToken) : [];
        var animalDict = animals.ToDictionary(a => a.Id, a => a.Tag.TagId);

        var dtos = items.Select(t => t.ToDto(animalDict.GetValueOrDefault(t.AnimalId))).ToList();
        return new PagedResult<MedicalTreatmentDto>(dtos, count, pageNumber, pageSize);
    }
}
