using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Registry;
using Farm360.Application.Reporting.Services;
using Farm360.Domain.Reporting;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

internal sealed class FarmScopedTestReport : ReportDefinition<StockRow>
{
    public override string Key => "test.farm";

    public override LocalizedText Title => new("Farm test", "খামার পরীক্ষা");

    public override ReportCategory Category => ReportCategory.Finance;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Batch(),
        ReportParameter.DateRange("period", @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<StockRow>> Columns =>
    [
        ReportColumn<StockRow>.Text("tag", "Tag", r => r.Tag),
    ];

    public DateOnly SeenToday { get; private set; }

    protected override Task<IReadOnlyList<StockRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        SeenToday = context.Today;
        context.FarmId().Should().NotBeEmpty();
        return Task.FromResult<IReadOnlyList<StockRow>>([]);
    }
}

public class ReportExecutionServiceTests
{
    private static readonly Guid FarmA = Guid.NewGuid();
    private static readonly Guid FarmB = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly string[] NonFinancePermissions = ["reports.view", "animals.view"];

    // 20:30 UTC on 15 Nov is 02:30 on 16 Nov in Dhaka.
    private static readonly DateTime UtcNow = new(2026, 11, 15, 20, 30, 0, DateTimeKind.Utc);

    private readonly FarmScopedTestReport _definition = new();
    private readonly IReportEnvironment _environment = Substitute.For<IReportEnvironment>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();

    private ReportExecutionService CreateService(params ReportFarmOption[] farms)
    {
        _environment.GetAccessibleFarmsAsync(Arg.Any<CancellationToken>()).Returns(farms);
        _environment.GetLocaleAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(ReportLocale.Default);
        _environment.GetUserDisplayNameAsync(Arg.Any<CancellationToken>()).Returns("Rafiq Ahmed (Manager)");
        _environment.DescribeAsync(ReportParameterType.Farm, FarmA, Arg.Any<CancellationToken>()).Returns("Green Valley Farm");
        _environment.DescribeAsync(ReportParameterType.Batch, BatchId, Arg.Any<CancellationToken>()).Returns("Eid 2026 Batch");

        _user.UserId.Returns(Guid.NewGuid());
        _permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var tenant = Substitute.For<ITenantService>();
        tenant.TenantName.Returns("Rahman Agro");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(UtcNow);

        return new ReportExecutionService(
            new ReportRegistry([_definition]),
            Substitute.For<ISender>(),
            tenant,
            _user,
            _permissions,
            clock,
            Substitute.For<IReportRunRepository>(),
            Substitute.For<IUnitOfWork>(),
            [],
            _environment,
            NullLogger<ReportExecutionService>.Instance);
    }

    [Fact]
    public async Task Header_names_the_farm_and_echoes_entities_by_name()
    {
        var service = CreateService(new ReportFarmOption(FarmA, "Green Valley Farm"), new ReportFarmOption(FarmB, "River Farm"));

        var data = await service.RunAsync(new ReportRequest("test.farm", new Dictionary<string, string?>
        {
            ["farmId"] = FarmA.ToString(),
            ["batchId"] = BatchId.ToString(),
        }));

        data.Meta.FarmName.Should().Be("Green Valley Farm");
        data.Meta.GeneratedByName.Should().Be("Rafiq Ahmed (Manager)");
        data.Meta.Parameters.Should().Contain(f => f.Label == "Farm" && f.Value == "Green Valley Farm");
        data.Meta.Parameters.Should().Contain(f => f.Label == "Batch" && f.Value == "Eid 2026 Batch");
    }

    [Fact]
    public async Task Today_and_the_printed_time_are_in_the_farm_time_zone()
    {
        var service = CreateService(new ReportFarmOption(FarmA, "Green Valley Farm"));

        var data = await service.RunAsync(new ReportRequest("test.farm", new Dictionary<string, string?>()));

        _definition.SeenToday.Should().Be(new DateOnly(2026, 11, 16), "it is already the 16th in Dhaka");
        data.Meta.GeneratedAtLocal.Should().Be("16-Nov-2026 02:30 BST (UTC+6)");
        data.Meta.Parameters.Should().Contain(f => f.Label == "Period" && f.Value == "16-Nov-2026");
    }

    [Fact]
    public async Task Blank_farm_is_auto_selected_only_when_the_user_has_one_farm()
    {
        var service = CreateService(new ReportFarmOption(FarmA, "Green Valley Farm"));

        var data = await service.RunAsync(new ReportRequest("test.farm", new Dictionary<string, string?>()));

        data.Meta.FarmName.Should().Be("Green Valley Farm");
    }

    [Fact]
    public async Task Blank_farm_with_several_farms_fails_validation_instead_of_picking_the_first()
    {
        var service = CreateService(new ReportFarmOption(FarmA, "Green Valley Farm"), new ReportFarmOption(FarmB, "River Farm"));

        var act = () => service.RunAsync(new ReportRequest("test.farm", new Dictionary<string, string?>()));

        (await act.Should().ThrowAsync<ReportParameterException>())
            .Which.ParameterName.Should().Be("farmId");
    }

    [Fact]
    public async Task Report_without_its_module_permission_is_refused_and_hidden()
    {
        var service = CreateService(new ReportFarmOption(FarmA, "Green Valley Farm"));
        _permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), ReportPermissions.FinanceView, Arg.Any<CancellationToken>())
            .Returns(false);
        _permissions.GetPermissionsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NonFinancePermissions);

        var act = () => service.RunAsync(new ReportRequest("test.farm", new Dictionary<string, string?>()));

        await act.Should().ThrowAsync<ReportAccessDeniedException>();
        (await service.GetCatalogAsync()).Should().BeEmpty();
    }

    [Fact]
    public void Time_zone_resolves_iana_and_windows_ids_and_falls_back()
    {
        ReportTimeZone.Resolve("Asia/Dhaka").GetUtcOffset(UtcNow).Should().Be(TimeSpan.FromHours(6));
        ReportTimeZone.Resolve("Bangladesh Standard Time").GetUtcOffset(UtcNow).Should().Be(TimeSpan.FromHours(6));
        ReportTimeZone.Resolve("Not/AZone", null, "").GetUtcOffset(UtcNow).Should().Be(TimeSpan.FromHours(6));
        ReportTimeZone.Label(ReportTimeZone.Default, UtcNow).Should().Be("BST (UTC+6)");
    }
}
