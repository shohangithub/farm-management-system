using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Dashboard.Queries;
using Farm360.Domain.Dashboard;
using Farm360.Domain.Dashboard.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Dashboard;

public class GetFarmSetupStatusQueryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    private static readonly string[] StepOrder =
        ["sheds", "location", "breeds", "animals", "weights", "inventory", "recipes", "feedChart", "plans", "accounts", "team"];

    private readonly Mock<IFarmSetupStatusRepository> _repository = new();
    private readonly Mock<ITenantService> _tenant = new();

    public GetFarmSetupStatusQueryTests()
    {
        _tenant.SetupGet(t => t.TenantId).Returns(TenantId);
    }

    private static FarmSetupCounts Empty() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, false, 1, 0, false);

    private async Task<Farm360.Application.Dashboard.DTOs.FarmSetupStatusDto> Run(FarmSetupCounts counts)
    {
        _repository.Setup(r => r.GetCountsAsync(TenantId, FarmId, It.IsAny<CancellationToken>())).ReturnsAsync(counts);
        var handler = new GetFarmSetupStatusQueryHandler(_repository.Object, _tenant.Object);
        return await handler.Handle(new GetFarmSetupStatusQuery(FarmId), CancellationToken.None);
    }

    [Fact]
    public async Task NewTenant_NothingDone_StepsInSetupOrder()
    {
        var result = await Run(Empty());

        result.FarmId.Should().Be(FarmId);
        result.Steps.Select(s => s.Key).Should().Equal(StepOrder);
        result.Steps.Should().OnlyContain(s => !s.IsDone);
        result.CompletedSteps.Should().Be(0);
        result.TotalSteps.Should().Be(11);
        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task PartialSetup_CountsDoneStepsAndPassesCountsThrough()
    {
        var result = await Run(Empty() with { Sheds = 2, Breeds = 5, Animals = 12, WeightRecords = 3 });

        result.CompletedSteps.Should().Be(4);
        result.Steps.Single(s => s.Key == "animals").Count.Should().Be(12);
        result.Steps.Single(s => s.Key == "inventory").IsDone.Should().BeFalse();
        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Inventory_NeedsFeedInStock_NotJustAnyItem()
    {
        var onlyMedicine = await Run(Empty() with { InventoryItemsInStock = 3, FeedItemsInStock = 0 });
        onlyMedicine.Steps.Single(s => s.Key == "inventory").IsDone.Should().BeFalse();

        var withFeed = await Run(Empty() with { InventoryItemsInStock = 3, FeedItemsInStock = 1 });
        withFeed.Steps.Single(s => s.Key == "inventory").IsDone.Should().BeTrue();
    }

    [Fact]
    public async Task Location_DoneOnlyWhenTheFarmHasCoordinates_RightAfterFarmStructure()
    {
        var without = await Run(Empty() with { Sheds = 1 });
        without.Steps.Single(s => s.Key == "location").IsDone.Should().BeFalse();
        without.Steps.Single(s => s.Key == "location").Count.Should().Be(0);

        var with = await Run(Empty() with { HasLocation = true });
        with.Steps.Single(s => s.Key == "location").IsDone.Should().BeTrue();
        with.Steps.Single(s => s.Key == "location").Count.Should().Be(1);
        with.CompletedSteps.Should().Be(1);
        with.Steps.Select(s => s.Key).ToList().IndexOf("location").Should().Be(1);
    }

    [Theory]
    [InlineData(1, 0, false)]
    [InlineData(2, 0, true)]
    [InlineData(1, 1, true)]
    public async Task Team_DoneWithSecondMemberOrPendingInvitation(int members, int pendingInvites, bool expected)
    {
        var result = await Run(Empty() with { TeamMembers = members, PendingInvitations = pendingInvites });

        result.Steps.Single(s => s.Key == "team").IsDone.Should().Be(expected);
    }

    [Fact]
    public async Task EverythingSetUp_IsComplete()
    {
        var result = await Run(new FarmSetupCounts(1, 1, 1, 1, 1, 1, 1, 1, 1, true, 2, 0, true));

        result.CompletedSteps.Should().Be(11);
        result.IsComplete.Should().BeTrue();
    }
}
