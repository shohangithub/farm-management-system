using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Jobs;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

public class CreateDailyFeedingEntriesCommandHandlerTests
{
    private readonly IAnimalFeedingPlanRepository _planRepository = Substitute.For<IAnimalFeedingPlanRepository>();
    private readonly IFeedingRuleSetRepository _ruleSetRepository = Substitute.For<IFeedingRuleSetRepository>();
    private readonly IDailyFeedingEntryRepository _entryRepository = Substitute.For<IDailyFeedingEntryRepository>();
    private readonly IAnimalRepository _animalRepository = Substitute.For<IAnimalRepository>();
    private readonly IFeedFormulaRepository _formulaRepository = Substitute.For<IFeedFormulaRepository>();
    private readonly IFeedIngredientRepository _feedIngredientRepository = Substitute.For<IFeedIngredientRepository>();
    private readonly Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository _inventoryRepository =
        Substitute.For<Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository>();
    private readonly IFeedAllocationService _allocationService = Substitute.For<IFeedAllocationService>();
    private readonly IAnimalFeedAllocationRepository _allocationRepository = Substitute.For<IAnimalFeedAllocationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CreateDailyFeedingEntriesCommandHandler> _logger =
        Substitute.For<ILogger<CreateDailyFeedingEntriesCommandHandler>>();

    private readonly CreateDailyFeedingEntriesCommandHandler _sut;

    public CreateDailyFeedingEntriesCommandHandlerTests()
    {
        _sut = new CreateDailyFeedingEntriesCommandHandler(
            _planRepository,
            _ruleSetRepository,
            _entryRepository,
            _animalRepository,
            _formulaRepository,
            _feedIngredientRepository,
            _inventoryRepository,
            _allocationService,
            _allocationRepository,
            _unitOfWork,
            _logger);
    }

    [Fact]
    public async Task Handle_WhenFeedingRuleSetIsInactive_ShouldNotCreateDailyFeedingEntry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var ruleSetId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var plan = new AnimalFeedingPlan(
            planId,
            tenantId,
            farmId,
            ruleSetId,
            FeedingPlanType.FixedQuantity,
            today.AddDays(-5),
            today.AddDays(30),
            animalId: Guid.NewGuid());

        var inactiveRuleSet = new FeedingRuleSet(
            ruleSetId,
            tenantId,
            "Inactive Rule",
            TargetAnimalType.Cattle,
            FeedingPurpose.Fattening,
            FeedingPlanType.FixedQuantity,
            isActive: false);
        inactiveRuleSet.AddRuleLine(0, 500, Guid.NewGuid(), 5.0m, 2.0m, 2);

        _planRepository.GetAllActivePlansAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AnimalFeedingPlan> { plan });

        _entryRepository.GetEntryPlanIdsAcrossTenantsByDateAsync(today, Arg.Any<CancellationToken>())
            .Returns(new HashSet<(Guid PlanId, Guid? RuleLineId)>());

        _ruleSetRepository.GetByIdAcrossTenantsAsync(ruleSetId, Arg.Any<CancellationToken>())
            .Returns(inactiveRuleSet);

        // Act
        await _sut.Handle(new CreateDailyFeedingEntriesCommand(), CancellationToken.None);

        // Assert
        await _entryRepository.DidNotReceive().AddAsync(Arg.Any<DailyFeedingEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFeedingRuleSetIsActive_ShouldCreateDailyFeedingEntry()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var ruleSetId = Guid.NewGuid();
        var formulaId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var plan = new AnimalFeedingPlan(
            planId,
            tenantId,
            farmId,
            ruleSetId,
            FeedingPlanType.FixedQuantity,
            today.AddDays(-5),
            today.AddDays(30),
            animalId: Guid.NewGuid());

        var activeRuleSet = new FeedingRuleSet(
            ruleSetId,
            tenantId,
            "Active Rule",
            TargetAnimalType.Cattle,
            FeedingPurpose.Fattening,
            FeedingPlanType.FixedQuantity,
            isActive: true);
        activeRuleSet.AddRuleLine(0, 500, formulaId, 5.0m, 2.0m, 2);

        _planRepository.GetAllActivePlansAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AnimalFeedingPlan> { plan });

        _entryRepository.GetEntryPlanIdsAcrossTenantsByDateAsync(today, Arg.Any<CancellationToken>())
            .Returns(new HashSet<(Guid PlanId, Guid? RuleLineId)>());

        _ruleSetRepository.GetByIdAcrossTenantsAsync(ruleSetId, Arg.Any<CancellationToken>())
            .Returns(activeRuleSet);

        // Act
        await _sut.Handle(new CreateDailyFeedingEntriesCommand(), CancellationToken.None);

        // Assert
        await _entryRepository.Received(1).AddAsync(Arg.Is<DailyFeedingEntry>(e =>
            e.FeedingPlanId == planId &&
            e.FormulaId == formulaId &&
            e.ExpectedKg == 5.0m), Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
