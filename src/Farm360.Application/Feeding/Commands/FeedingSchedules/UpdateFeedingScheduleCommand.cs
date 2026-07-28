using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedingSchedules;

public sealed record UpdateFeedingScheduleCommand(
    Guid Id,
    string Title,
    Guid FormulaId,
    decimal TargetQuantityKgPerHead,
    ScheduleFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate = null,
    bool IsActive = true,
    string? Notes = null) : IRequest;

public sealed class UpdateFeedingScheduleCommandValidator : AbstractValidator<UpdateFeedingScheduleCommand>
{
    public UpdateFeedingScheduleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FormulaId).NotEmpty();
        RuleFor(x => x.TargetQuantityKgPerHead).GreaterThan(0);
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.StartDate).NotEmpty();
    }
}

public sealed class UpdateFeedingScheduleCommandHandler : IRequestHandler<UpdateFeedingScheduleCommand>
{
    private readonly IFeedingScheduleRepository _scheduleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFeedingScheduleCommandHandler(
        IFeedingScheduleRepository scheduleRepository,
        IUnitOfWork unitOfWork)
    {
        _scheduleRepository = scheduleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateFeedingScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Feeding schedule with ID '{request.Id}' was not found.");

        schedule.UpdateSchedule(
            request.Title,
            request.FormulaId,
            request.TargetQuantityKgPerHead,
            request.Frequency,
            request.StartDate,
            request.EndDate,
            request.Notes);

        schedule.SetActiveStatus(request.IsActive);

        _scheduleRepository.Update(schedule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
