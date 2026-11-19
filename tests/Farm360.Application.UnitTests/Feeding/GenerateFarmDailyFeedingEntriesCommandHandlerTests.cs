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

public class GenerateFarmDailyFeedingEntriesCommandHandlerTests
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
    private readonly ILogger<GenerateFarmDailyFeedingEntriesCommandHandler> _logger =
        Substitute.For<ILogger<GenerateFarmDailyFeedingEntriesCommandHandler>>();

    private readonly GenerateFarmDailyFeedingEntriesCommandHandler _sut;

    public GenerateFarmDailyFeedingEntriesCommandHandlerTests()
    {
        _animalRepository.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Farm360.Domain.Livestock.Animal>());
        _sut = new GenerateFarmDailyFeedingEntriesCommandHandler(
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

        _planRepository.GetActivePlansByFarmAsync(tenantId, farmId, Arg.Any<CancellationToken>())
            .Returns(new List<AnimalFeedingPlan> { plan });

        _entryRepository.GetEntryKeysAsync(tenantId, farmId, today, Arg.Any<CancellationToken>())
            .Returns(new HashSet<(Guid PlanId, Guid? RuleLineId)>());

        _ruleSetRepository.GetByIdAcrossTenantsAsync(ruleSetId, Arg.Any<CancellationToken>())
            .Returns(inactiveRuleSet);

        // Act
        await _sut.Handle(new GenerateFarmDailyFeedingEntriesCommand(tenantId, farmId, today), CancellationToken.None);

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

        _planRepository.GetActivePlansByFarmAsync(tenantId, farmId, Arg.Any<CancellationToken>())
            .Returns(new List<AnimalFeedingPlan> { plan });

        _entryRepository.GetEntryKeysAsync(tenantId, farmId, today, Arg.Any<CancellationToken>())
            .Returns(new HashSet<(Guid PlanId, Guid? RuleLineId)>());

        _ruleSetRepository.GetByIdAcrossTenantsAsync(ruleSetId, Arg.Any<CancellationToken>())
            .Returns(activeRuleSet);

        // Act
        await _sut.Handle(new GenerateFarmDailyFeedingEntriesCommand(tenantId, farmId, today), CancellationToken.None);

        // Assert
        await _entryRepository.Received(1).AddAsync(Arg.Is<DailyFeedingEntry>(e =>
            e.FeedingPlanId == planId &&
            e.FormulaId == formulaId &&
            e.ExpectedKg == 5.0m), Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTheDayWasAlreadyGenerated_ShouldNotCreateADuplicate()
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var ruleSetId = Guid.NewGuid();
        var today = new DateOnly(2026, 11, 17);
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), tenantId, farmId, ruleSetId, FeedingPlanType.FixedQuantity,
            today.AddDays(-5), today.AddDays(30), animalId: Guid.NewGuid());
        var ruleSet = new FeedingRuleSet(ruleSetId, tenantId, "Active", TargetAnimalType.Cattle, FeedingPurpose.Fattening,
            FeedingPlanType.FixedQuantity, isActive: true);
        ruleSet.AddRuleLine(0, 500, Guid.NewGuid(), 5.0m, 2.0m, 2);
        var lineId = ruleSet.Lines.First().Id;

        _planRepository.GetActivePlansByFarmAsync(tenantId, farmId, Arg.Any<CancellationToken>()).Returns(new List<AnimalFeedingPlan> { plan });
        _entryRepository.GetEntryKeysAsync(tenantId, farmId, today, Arg.Any<CancellationToken>())
            .Returns(new HashSet<(Guid PlanId, Guid? RuleLineId)> { (plan.Id, lineId) });
        _ruleSetRepository.GetByIdAcrossTenantsAsync(ruleSetId, Arg.Any<CancellationToken>()).Returns(ruleSet);

        var created = await _sut.Handle(new GenerateFarmDailyFeedingEntriesCommand(tenantId, farmId, today), CancellationToken.None);

        created.Should().Be(0);
        await _entryRepository.DidNotReceive().AddAsync(Arg.Any<DailyFeedingEntry>(), Arg.Any<CancellationToken>());
    }
}
