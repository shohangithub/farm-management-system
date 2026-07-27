using Farm360.Application.Health.DTOs;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.SpecializedReports;

public sealed record GetMilkWithdrawalAnimalsQuery(Guid FarmId) : IRequest<IReadOnlyList<MilkWithdrawalDto>>;

internal sealed class GetMilkWithdrawalAnimalsQueryHandler(IMedicalTreatmentRepository repository)
    : IRequestHandler<GetMilkWithdrawalAnimalsQuery, IReadOnlyList<MilkWithdrawalDto>>
{
    public async Task<IReadOnlyList<MilkWithdrawalDto>> Handle(GetMilkWithdrawalAnimalsQuery request, CancellationToken cancellationToken)
    {
        var activeWithdrawals = await repository.GetActiveMilkWithdrawalsAsync(request.FarmId, cancellationToken);

        var dtos = activeWithdrawals.Select(x => new MilkWithdrawalDto(
            x.Treatment.AnimalId,
            x.AnimalTag,
            x.Treatment.Id,
            x.Treatment.MedicationName,
            x.Treatment.StartDate,
            x.Treatment.WithdrawalPeriod.MilkDays,
            x.Treatment.StartDate.AddDays(x.Treatment.WithdrawalPeriod.MilkDays)
        )).ToList();

        return dtos;
    }
}
