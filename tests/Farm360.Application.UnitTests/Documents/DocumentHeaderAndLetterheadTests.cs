using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Documents;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Organizations.ValueObjects;
using Farm360.Domain.Tenancy.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Documents;

public class DocumentHeaderAndLetterheadTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    // ── Language resolution ─────────────────────────────────────────────────

    [Theory]
    [InlineData("bn", ReportLanguagePreference.English, ReportLanguage.Bn)]
    [InlineData("BN", ReportLanguagePreference.English, ReportLanguage.Bn)]
    [InlineData("en", ReportLanguagePreference.Bangla, ReportLanguage.En)]
    [InlineData(null, ReportLanguagePreference.Bangla, ReportLanguage.Bn)]
    [InlineData("", ReportLanguagePreference.English, ReportLanguage.En)]
    [InlineData("fr", ReportLanguagePreference.Bangla, ReportLanguage.Bn)]
    public void Query_string_language_overrides_the_farm_default(string? requested, ReportLanguagePreference farmDefault, ReportLanguage expected)
    {
        DocumentHeaderBuilder.ResolveLanguage(requested, farmDefault).Should().Be(expected);
    }

    [Fact]
    public async Task Header_takes_the_farm_rules_letterhead_and_local_time()
    {
        var environment = Substitute.For<IReportEnvironment>();
        environment.GetLetterheadAsync(FarmId, Arg.Any<CancellationToken>())
            .Returns(new ReportLetterhead("Rahman Agro", "Savar · 01711-000000", "/uploads/organizations/logo.png", DocumentTestKit.TinyPng));
        environment.DescribeAsync(ReportParameterType.Farm, FarmId, Arg.Any<CancellationToken>()).Returns("Green Valley Farm");
        environment.GetLocaleAsync(FarmId, Arg.Any<CancellationToken>()).Returns(ReportLocale.Default);
        environment.GetUserDisplayNameAsync(Arg.Any<CancellationToken>()).Returns("Rafiq Ahmed (Manager)");

        var rules = Substitute.For<IFarmBusinessRulesProvider>();
        rules.For(TenantId, FarmId).Returns(new FarmBusinessRules
        {
            DefaultReportLanguage = ReportLanguagePreference.Bangla,
            BengaliNumeralsByDefault = true,
        });

        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantId);
        tenant.TenantName.Returns("Tenant name");

        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 23, 20, 30, 0, DateTimeKind.Utc));

        var builder = new DocumentHeaderBuilder(environment, rules, tenant, clock);

        var byRule = await builder.BuildAsync(FarmId, null, null, CancellationToken.None);
        byRule.Language.Should().Be(ReportLanguage.Bn);
        byRule.BengaliNumerals.Should().BeTrue();
        byRule.OrganizationName.Should().Be("Rahman Agro");
        byRule.FarmName.Should().Be("Green Valley Farm");
        byRule.ContactLine.Should().Be("Savar · 01711-000000");
        byRule.Logo.Should().NotBeNull();
        byRule.Today.Should().Be(new DateOnly(2026, 11, 24), "20:30 UTC is already the next day in Dhaka");
        byRule.Money(12_500m).Should().Be("১২,৫০০.০০");

        var overridden = await builder.BuildAsync(FarmId, "en", false, CancellationToken.None);
        overridden.Language.Should().Be(ReportLanguage.En);
        overridden.BengaliNumerals.Should().BeFalse();
        overridden.Money(12_500m).Should().Be("12,500.00");
    }

    // ── Letterhead: logo and contact line ───────────────────────────────────

    private sealed class EnvironmentFixture
    {
        public IOrganizationRepository Organizations { get; } = Substitute.For<IOrganizationRepository>();
        public IFileStorageService Files { get; } = Substitute.For<IFileStorageService>();

        public ReportEnvironment Create()
        {
            var tenant = Substitute.For<ITenantService>();
            tenant.TenantId.Returns(TenantId);

            return new ReportEnvironment(
                tenant,
                Substitute.For<ICurrentUserService>(),
                Substitute.For<IUserProfileService>(),
                Substitute.For<IFarmRepository>(),
                Substitute.For<IBranchRepository>(),
                Organizations,
                Substitute.For<ITenantRepository>(),
                Substitute.For<IFarmAccountingSettingsRepository>(),
                Substitute.For<IAnimalRepository>(),
                Substitute.For<IAnimalBatchRepository>(),
                Substitute.For<IShedRepository>(),
                Substitute.For<IBreedRepository>(),
                Substitute.For<IInventoryItemRepository>(),
                Files);
        }
    }

    private static Organization NewOrganization(string? logoUrl) =>
        Organization.Create(
            TenantId, "Rahman Agro", logoUrl, "info@rahmanagro.com", "01711-000000", null, null, null,
            "BDT", "Asia/Dhaka", "en", Address.Create("House 12, Road 4", "Savar", "Dhaka", "Bangladesh", "1340"),
            BusinessType.Farm);

    [Fact]
    public async Task Letterhead_carries_the_organisation_contact_line_and_logo_and_reads_the_logo_once()
    {
        var fixture = new EnvironmentFixture();
        fixture.Organizations.GetAllByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<Organization> { NewOrganization("/uploads/organizations/logo.png") });
        fixture.Files.ReadFileAsync("/uploads/organizations/logo.png", Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(DocumentTestKit.TinyPng);

        var environment = fixture.Create();
        var first = await environment.GetLetterheadAsync(null, CancellationToken.None);
        var second = await environment.GetLetterheadAsync(null, CancellationToken.None);

        first.OrganizationName.Should().Be("Rahman Agro");
        first.ContactLine.Should().Be("House 12, Road 4, Savar, Dhaka, 1340, Bangladesh · 01711-000000 · info@rahmanagro.com");
        first.LogoUrl.Should().Be("/uploads/organizations/logo.png");
        first.Logo.Should().Equal(DocumentTestKit.TinyPng);
        second.Should().BeSameAs(first);
        await fixture.Files.Received(1).ReadFileAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_logo_file_leaves_the_letterhead_without_a_logo()
    {
        var fixture = new EnvironmentFixture();
        fixture.Organizations.GetAllByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<Organization> { NewOrganization("/uploads/organizations/gone.png") });
        fixture.Files.ReadFileAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        var letterhead = await fixture.Create().GetLetterheadAsync(null, CancellationToken.None);

        letterhead.OrganizationName.Should().Be("Rahman Agro");
        letterhead.Logo.Should().BeNull();
    }

    [Fact]
    public async Task A_logo_url_outside_the_organisation_uploads_is_never_read()
    {
        var fixture = new EnvironmentFixture();
        fixture.Organizations.GetAllByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<Organization> { NewOrganization("https://example.com/logo.png") });

        var letterhead = await fixture.Create().GetLetterheadAsync(null, CancellationToken.None);

        letterhead.Logo.Should().BeNull();
        await fixture.Files.DidNotReceiveWithAnyArgs().ReadFileAsync(default!, default, default);
    }

    [Fact]
    public async Task No_organisation_means_no_letterhead_details()
    {
        var fixture = new EnvironmentFixture();
        fixture.Organizations.GetAllByTenantAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new List<Organization>());

        var letterhead = await fixture.Create().GetLetterheadAsync(null, CancellationToken.None);

        letterhead.Should().BeSameAs(ReportLetterhead.None);
    }
}
