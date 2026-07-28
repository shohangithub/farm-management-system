using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedingSchedules;

public sealed record CreateFeedingScheduleCommand(
    Guid FarmId,
    Guid FormulaId,
    string Title,
    decimal TargetQuantityKgPerHead,
    ScheduleFrequency Frequency,
    DateOnly StartDate,
    Guid? ShedId = null,
    Guid? PenId = null,
    Guid? BatchId = null,
    DateOnly? EndDate = null,
    string? Notes = null) : IRequest<Guid>;

public sealed class CreateFeedingScheduleCommandValidator : AbstractValidator<CreateFeedingScheduleCommand>
{
    public CreateFeedingScheduleCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.FormulaId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetQuantityKgPerHead).GreaterThan(0);
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.StartDate).NotEmpty();
    }
}

public sealed class CreateFeedingScheduleCommandHandler : IRequestHandler<CreateFeedingScheduleCommand, Guid>
{
    private readonly IFeedingScheduleRepository _scheduleRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFeedingScheduleCommandHandler(
        IFeedingScheduleRepository scheduleRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _scheduleRepository = scheduleRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateFeedingScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = new FeedingSchedule(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.FarmId,
            request.FormulaId,
            request.Title,
            request.TargetQuantityKgPerHead,
            request.Frequency,
            request.StartDate,
            request.ShedId,
            request.PenId,
            request.BatchId,
            request.EndDate,
            request.Notes);

        await _scheduleRepository.AddAsync(schedule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return schedule.Id;
    }
}
