using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Tasks;

/// <summary>Marks one or more health tasks as given (medicine/vaccine) or done (care task).</summary>
public sealed record CompleteHealthTasksCommand(
    IReadOnlyList<Guid> EventIds,
    DateOnly Date,
    string? Notes
) : IRequest, ITransactionalCommand;

public sealed class CompleteHealthTasksCommandValidator : AbstractValidator<CompleteHealthTasksCommand>
{
    public CompleteHealthTasksCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(c => c.EventIds)
            .NotEmpty().WithMessage("Choose at least one task.")
            .Must(ids => ids.Count <= 500).WithMessage("Up to 500 tasks can be marked at once.");
        RuleFor(c => c.Notes).MaximumLength(1000);
        RuleFor(c => c.Date)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("The date cannot be in the future.");
    }
}

internal sealed class CompleteHealthTasksCommandHandler(IHealthTaskCompletionService completion)
    : IRequestHandler<CompleteHealthTasksCommand>
{
    public Task Handle(CompleteHealthTasksCommand request, CancellationToken cancellationToken) =>
        completion.CompleteAsync(request.EventIds, request.Date, request.Notes, cancellationToken);
}

/// <summary>Skips a health task with a reason; a repeating task is scheduled again from the skip date.</summary>
public sealed record SkipHealthTaskCommand(Guid EventId, DateOnly Date, string Reason) : IRequest, ITransactionalCommand;

public sealed class SkipHealthTaskCommandValidator : AbstractValidator<SkipHealthTaskCommand>
{
    public SkipHealthTaskCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Please give a reason for skipping.")
            .MaximumLength(500);
        RuleFor(c => c.Date)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("The date cannot be in the future.");
    }
}

internal sealed class SkipHealthTaskCommandHandler(IHealthTaskCompletionService completion)
    : IRequestHandler<SkipHealthTaskCommand>
{
    public Task Handle(SkipHealthTaskCommand request, CancellationToken cancellationToken) =>
        completion.SkipAsync(request.EventId, request.Date, request.Reason, cancellationToken);
}

/// <summary>Moves a health task that is still due to another day.</summary>
public sealed record RescheduleHealthTaskCommand(Guid EventId, DateOnly NewDate) : IRequest;

public sealed class RescheduleHealthTaskCommandValidator : AbstractValidator<RescheduleHealthTaskCommand>
{
    public RescheduleHealthTaskCommandValidator()
    {
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.NewDate).NotEmpty();
    }
}

internal sealed class RescheduleHealthTaskCommandHandler(
    IVaccinationRepository vaccinations,
    IAnimalRepository animals,
    IUnitOfWork unitOfWork,
    IFarmClock? clock = null) : IRequestHandler<RescheduleHealthTaskCommand>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task Handle(RescheduleHealthTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await vaccinations.GetEventByIdAsync(request.EventId, cancellationToken)
            ?? throw new KeyNotFoundException($"Health task '{request.EventId}' was not found.");
        var animal = await animals.GetByIdAsync(task.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"The animal for this health task ({task.AnimalId}) was not found.");
        animal.EnsureOnFarm("a health task");

        task.Reschedule(request.NewDate, _clock.TodayForFarm(task.TenantId, animal.FarmId));
        vaccinations.UpdateEvent(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
