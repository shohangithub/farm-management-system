using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.VetVisits;

public sealed record VetVisitDetailDto(
    Guid Id,
    Guid FarmId,
    string VetName,
    DateOnly VisitDate,
    string VisitType,
    int VisitTypeId,
    string? Purpose,
    string? Findings,
    string? Recommendations,
    decimal? CostBdt,
    DateOnly? NextVisitDate,
    DateTime CreatedAt
);

public sealed record GetVetVisitDetailQuery(Guid Id) : IRequest<VetVisitDetailDto>;

internal sealed class GetVetVisitDetailQueryHandler : IRequestHandler<GetVetVisitDetailQuery, VetVisitDetailDto>
{
    private readonly IVetVisitRepository _repository;

    public GetVetVisitDetailQueryHandler(IVetVisitRepository repository)
    {
        _repository = repository;
    }

    public async Task<VetVisitDetailDto> Handle(GetVetVisitDetailQuery request, CancellationToken cancellationToken)
    {
        var visit = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(VetVisit), request.Id);

        return new VetVisitDetailDto(
            visit.Id,
            visit.FarmId,
            visit.VetName,
            visit.VisitDate,
            visit.VisitType.ToString(),
            (int)visit.VisitType,
            visit.Purpose,
            visit.Findings,
            visit.Recommendations,
            visit.CostBdt,
            visit.NextVisitDate,
            visit.CreatedAt
        );
    }
}
