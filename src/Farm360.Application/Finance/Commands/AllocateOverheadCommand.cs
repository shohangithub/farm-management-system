using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Finance.Commands;

/// <summary>
/// Allocates a farm's labour and overhead for a period onto its animals (docs/32 GAP-2).
/// </summary>
/// <remarks>
/// Intended to run monthly once the period's expenses are posted, and re-runnable for a
/// corrected or re-opened month. <paramref name="IsBackfill"/> only marks the rows produced;
/// it changes no arithmetic.
/// </remarks>
public sealed record AllocateOverheadCommand(
    Guid FarmId,
    DateOnly From,
    DateOnly To,
    bool IsBackfill = false) : IRequest<OverheadAllocationResult>;

public sealed class AllocateOverheadCommandValidator : AbstractValidator<AllocateOverheadCommand>
{
    public AllocateOverheadCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("The period end cannot precede its start.");
    }
}

public sealed class AllocateOverheadCommandHandler
    : IRequestHandler<AllocateOverheadCommand, OverheadAllocationResult>
{
    private readonly IOverheadAllocationService _service;

    public AllocateOverheadCommandHandler(IOverheadAllocationService service)
    {
        _service = service;
    }

    public Task<OverheadAllocationResult> Handle(AllocateOverheadCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _service.AllocateAsync(
            request.FarmId, request.From, request.To, request.IsBackfill, cancellationToken);
    }
}
