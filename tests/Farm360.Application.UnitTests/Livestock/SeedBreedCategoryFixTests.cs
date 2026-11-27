using Farm360.Application.Demo;
using Farm360.Application.Livestock;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Livestock;

/// <summary>
/// Breed categories: seeds use Indigenous / Exotic / Crossbred, and the startup fix corrects only
/// rows that carry a seed's exact fingerprint with an old species-style category.
/// </summary>
public sealed class SeedBreedCategoryFixTests
{
    private static DemoBreedSpec Spec(string name) => DemoSeedPlan.BreedSpecs.Single(b => b.Name == name);

    private static SeedBreedCategoryFix.StoredBreed SeededRow(string name, string category)
    {
        var spec = Spec(name);
        return new SeedBreedCategoryFix.StoredBreed(Guid.NewGuid(), spec.Name, category, spec.Origin, spec.SeedDescription);
    }

    [Fact]
    public void DemoBreeds_UseTheBreedScreenCategories()
    {
        DemoSeedPlan.BreedSpecs.Should().OnlyContain(b => BreedCategories.IsKnown(b.Category));
        Spec("Local (Deshi)").Category.Should().Be(BreedCategories.Indigenous);
        Spec("Sahiwal Cross").Category.Should().Be(BreedCategories.Crossbred);
        Spec("Brahman Cross").Category.Should().Be(BreedCategories.Crossbred);
        Spec("Black Bengal Goat").Category.Should().Be(BreedCategories.Indigenous);
    }

    [Fact]
    public void DemoPlan_StillUsesTheSameBreedList()
    {
        DemoSeedPlan.Create(new DateOnly(2026, 11, 26)).Breeds.Should().Equal(DemoSeedPlan.BreedSpecs);
    }

    [Fact]
    public void Plan_CorrectsSeededRowsWithTheOldCategories()
    {
        var deshi = SeededRow("Local (Deshi)", "Cattle");
        var sahiwal = SeededRow("Sahiwal Cross", "Cattle");
        var goat = SeededRow("Black Bengal Goat", "Goat");

        var fixes = SeedBreedCategoryFix.Plan([deshi, sahiwal, goat]);

        fixes.Should().HaveCount(3);
        fixes[deshi.Id].Should().Be(BreedCategories.Indigenous);
        fixes[sahiwal.Id].Should().Be(BreedCategories.Crossbred);
        fixes[goat.Id].Should().Be(BreedCategories.Indigenous);
    }

    [Fact]
    public void Plan_LeavesTenantCreatedBreedsAlone_EvenWithASeedName()
    {
        // Same name and an odd category, but the tenant wrote their own description / origin.
        var ownDescription = new SeedBreedCategoryFix.StoredBreed(Guid.NewGuid(), "Sahiwal Cross", "Cattle", Spec("Sahiwal Cross").Origin, "Our own Sahiwal line");
        var ownOrigin = new SeedBreedCategoryFix.StoredBreed(Guid.NewGuid(), "Local (Deshi)", "Cattle", "Rangpur", Spec("Local (Deshi)").SeedDescription);
        var unknownName = new SeedBreedCategoryFix.StoredBreed(Guid.NewGuid(), "Shahiwal X", "Cattle", "Bangladesh", "Shahiwal X - Eid.");

        SeedBreedCategoryFix.Plan([ownDescription, ownOrigin, unknownName]).Should().BeEmpty();
    }

    [Fact]
    public void Plan_LeavesRowsThatAlreadyHaveABreedScreenCategory()
    {
        // A tenant who re-categorised a seeded breed (to any UI category) keeps their choice; reruns are no-ops.
        var recategorised = SeededRow("Brahman Cross", BreedCategories.Exotic);
        var alreadyFixed = SeededRow("Local (Deshi)", BreedCategories.Indigenous);

        SeedBreedCategoryFix.Plan([recategorised, alreadyFixed]).Should().BeEmpty();
    }

    [Fact]
    public void SeedBreedNames_ListsEveryDemoBreed()
    {
        SeedBreedCategoryFix.SeedBreedNames().Should().BeEquivalentTo(DemoSeedPlan.BreedSpecs.Select(b => b.Name));
    }
}
