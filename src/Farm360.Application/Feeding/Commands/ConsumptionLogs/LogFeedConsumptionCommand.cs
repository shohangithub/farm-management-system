using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.ConsumptionLogs;

public record ConsumptionIngredientDetailRequest(Guid IngredientId, decimal OfferedKg, decimal RefusalKg);

public sealed record LogFeedConsumptionCommand(
    Guid FarmId,
    Guid FormulaId,
    DateOnly LogDate,
    int HeadCount,
    decimal TotalFeedOfferedKg,
    decimal TotalRefusalKg,
    Guid? ShedId = null,
    Guid? PenId = null,
    Guid? BatchId = null,
    string? Notes = null,
    IReadOnlyList<ConsumptionIngredientDetailRequest>? Details = null) : IRequest<Guid>;

public sealed class LogFeedConsumptionCommandValidator : AbstractValidator<LogFeedConsumptionCommand>
{
    public LogFeedConsumptionCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.FormulaId).NotEmpty();
        RuleFor(x => x.LogDate).NotEmpty();
        RuleFor(x => x.HeadCount).GreaterThan(0);
        RuleFor(x => x.TotalFeedOfferedKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalRefusalKg).GreaterThanOrEqualTo(0);
    }
}

public sealed class LogFeedConsumptionCommandHandler : IRequestHandler<LogFeedConsumptionCommand, Guid>
{
    private readonly IFeedConsumptionLogRepository _logRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public LogFeedConsumptionCommandHandler(
        IFeedConsumptionLogRepository logRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository ingredientRepository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _logRepository = logRepository;
        _formulaRepository = formulaRepository;
        _ingredientRepository = ingredientRepository;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(LogFeedConsumptionCommand request, CancellationToken cancellationToken)
    {
        var formula = await _formulaRepository.GetByIdAsync(request.FormulaId, cancellationToken);
        decimal costPerKg = formula?.TotalCostPerKgBdt ?? 0;

        var log = new FeedConsumptionLog(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.FarmId,
            request.FormulaId,
            request.LogDate,
            request.HeadCount,
            request.TotalFeedOfferedKg,
            request.TotalRefusalKg,
            costPerKg,
            request.ShedId,
            request.PenId,
            request.BatchId,
            null, // feedingPlanId
            _currentUserService.UserId?.ToString(),
            request.Notes);

        if (request.Details != null && request.Details.Count > 0)
        {
            var ingredients = await _ingredientRepository.GetAllAsync(_tenantService.TenantId, true, cancellationToken);
            var ingDict = ingredients.ToDictionary(i => i.Id);

            foreach (var d in request.Details)
            {
                decimal unitCost = ingDict.TryGetValue(d.IngredientId, out var ing) ? ing.UnitCostBdt : 0;
                log.AddDetail(d.IngredientId, d.OfferedKg, d.RefusalKg, unitCost);
            }
        }

        await _logRepository.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish event for Inventory integration
        var domainEvent = log.DomainEvents.OfType<Farm360.Domain.Feeding.Events.FeedConsumptionLoggedEvent>().FirstOrDefault();
        if (domainEvent != null)
        {
            await _publisher.Publish(new Farm360.Application.Inventory.EventHandlers.FeedConsumptionLoggedNotification(domainEvent), cancellationToken);
        }

        return log.Id;
    }
}
