using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.Tasks;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Health.Programmes;

// ══════════════════════════════════════════════════════════════════════════════
// Ready-made template: "Cattle bought at market – 108 days"
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>Adds the vet's 108-day market-cattle programme to this tenant's protocols.</summary>
public sealed record AddMarketCattleProgrammeCommand(
    int RepeatEveryDays = MarketCattleProgrammeTemplate.DefaultRepeatEveryDays,
    bool AutoStartForPurchased = true) : IRequest<Guid>;

public sealed class AddMarketCattleProgrammeCommandValidator : AbstractValidator<AddMarketCattleProgrammeCommand>
{
    public AddMarketCattleProgrammeCommandValidator()
    {
        RuleFor(c => c.RepeatEveryDays).InclusiveBetween(1, 365)
            .WithMessage("The repeat must be between 1 and 365 days.");
    }
}

internal sealed class AddMarketCattleProgrammeCommandHandler(
    IVaccinationRepository repository,
    ITenantService tenantService,
    IUnitOfWork unitOfWork) : IRequestHandler<AddMarketCattleProgrammeCommand, Guid>
{
    public async Task<Guid> Handle(AddMarketCattleProgrammeCommand request, CancellationToken cancellationToken)
    {
        var protocol = MarketCattleProgrammeTemplate.Build(tenantService.TenantId, request.RepeatEveryDays, request.AutoStartForPurchased);
        repository.AddProtocol(protocol);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return protocol.Id;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Programmes that start by themselves for purchased animals
// ══════════════════════════════════════════════════════════════════════════════

public sealed record AutoStartProgrammeDto(Guid Id, string Title, int TaskCount, int SpanDays);

/// <summary>The active protocols that will start automatically for a purchased animal of this species.</summary>
public sealed record GetAutoStartProgrammesQuery(AnimalSpecies Species) : IRequest<IReadOnlyList<AutoStartProgrammeDto>>;

internal sealed class GetAutoStartProgrammesQueryHandler(IVaccinationRepository repository)
    : IRequestHandler<GetAutoStartProgrammesQuery, IReadOnlyList<AutoStartProgrammeDto>>
{
    public async Task<IReadOnlyList<AutoStartProgrammeDto>> Handle(GetAutoStartProgrammesQuery request, CancellationToken cancellationToken)
    {
        var protocols = await repository.GetAutoStartProtocolsAsync(request.Species, cancellationToken);
        return protocols
            .Select(p => new AutoStartProgrammeDto(p.Id, p.Title, p.Steps.Count, p.SpanDays))
            .ToList();
    }
}

/// <summary>
/// Published by animal registration for a purchased animal unless the user opted out for it: every
/// active "start automatically for new purchased animals" protocol of its species is started from the
/// acquisition date.
/// </summary>
public sealed record PurchasedAnimalRegisteredNotification(
    Guid TenantId,
    Guid FarmId,
    Guid AnimalId,
    AnimalSpecies Species,
    DateOnly AcquisitionDate) : INotification;

public sealed class AutoStartHealthProgrammesHandler(
    IVaccinationRepository repository,
    IAnimalRepository animals,
    IFarmBusinessRulesProvider rules,
    IUnitOfWork unitOfWork,
    ILogger<AutoStartHealthProgrammesHandler> logger,
    IFarmClock? clock = null) : INotificationHandler<PurchasedAnimalRegisteredNotification>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task Handle(PurchasedAnimalRegisteredNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var protocols = await repository.GetAutoStartProtocolsAsync(notification.Species, cancellationToken);
        if (protocols.Count == 0)
            return;

        var animal = await animals.GetByIdAsync(notification.AnimalId, cancellationToken);
        if (animal is null || animal.HasLeftFarm)
            return;

        var weight = DoseWeight.For(animal.LatestWeightKg, rules.For(notification.TenantId, notification.FarmId).FallbackAnimalWeightKg);
        // Registered some days after purchase: the last week stays (to record what was done); older
        // one-off steps are not created as a flood of overdue work.
        var today = _clock.TodayForFarm(notification.TenantId, notification.FarmId);

        var events = new List<VaccinationEvent>();
        foreach (var protocol in protocols)
        {
            events.AddRange(HealthTaskScheduling.BuildEvents(
                protocol, notification.TenantId, notification.AnimalId, notification.AcquisitionDate, weight,
                skipPastOneOffsBefore: today.AddDays(-7), today: today));
        }

        if (events.Count == 0)
            return;

        repository.AddEvents(events);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Started {ProtocolCount} health programme(s) ({TaskCount} tasks) for purchased animal {AnimalId}.",
                protocols.Count, events.Count, notification.AnimalId);
        }
    }
}
