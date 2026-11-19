using System;
using Farm360.Application.Reporting.Definitions.Health;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Health.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class VaccinationComplianceTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateOnly Due = new(2026, 11, 5);

    [Fact]
    public void Given_within_grace_is_on_time() =>
        VaccinationComplianceReportDefinition.Classify(VaccinationStatus.Completed, Due, Due.AddDays(7), Today, 7)
            .Should().Be(VaccinationComplianceStatus.GivenOnTime);

    [Fact]
    public void Given_after_grace_is_late() =>
        VaccinationComplianceReportDefinition.Classify(VaccinationStatus.Completed, Due, Due.AddDays(8), Today, 7)
            .Should().Be(VaccinationComplianceStatus.GivenLate);

    [Fact]
    public void Not_given_and_past_due_is_overdue_even_if_still_marked_scheduled() =>
        VaccinationComplianceReportDefinition.Classify(VaccinationStatus.Scheduled, Due, null, Today, 7)
            .Should().Be(VaccinationComplianceStatus.Overdue);

    [Fact]
    public void Not_given_and_not_yet_due_is_due() =>
        VaccinationComplianceReportDefinition.Classify(VaccinationStatus.Scheduled, Today.AddDays(3), null, Today, 7)
            .Should().Be(VaccinationComplianceStatus.Due);

    [Fact]
    public void Cancelled_wins_over_everything() =>
        VaccinationComplianceReportDefinition.Classify(VaccinationStatus.Cancelled, Due, null, Today, 7)
            .Should().Be(VaccinationComplianceStatus.Cancelled);

    [Fact]
    public void Compliance_is_on_time_over_doses_already_due_in_the_period()
    {
        var period = new DateRange(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        VaccinationComplianceReportRow Row(DateOnly due, VaccinationComplianceStatus status, DateOnly? given = null) =>
            new("FMD", "FMD", "BD-1", due, given, status, status.ToString(), null, "—");

        var summary = VaccinationComplianceReportDefinition.Summarise(
        [
            Row(Due, VaccinationComplianceStatus.GivenOnTime, Due),
            Row(Due, VaccinationComplianceStatus.GivenOnTime, Due),
            Row(Due, VaccinationComplianceStatus.GivenLate, Due.AddDays(20)),
            Row(Due, VaccinationComplianceStatus.Overdue),
            Row(Due, VaccinationComplianceStatus.Cancelled),
            Row(Today.AddDays(5), VaccinationComplianceStatus.Due), // not yet due: not a failure
        ], period, Today);

        summary.DueInPeriod.Should().Be(4);
        summary.GivenOnTime.Should().Be(2);
        summary.CompliancePercent.Should().Be(50.0m);
        summary.Overdue.Should().Be(1);
        summary.Upcoming.Should().Be(1);
    }

    [Fact]
    public void A_row_without_a_given_date_prints_no_date()
    {
        var row = new VaccinationComplianceReportRow("FMD", "FMD", "BD-1", Due, null, VaccinationComplianceStatus.Overdue, "Overdue", 11, "—");

        ReportValueFormatter.Format(row.GivenDate, ReportColumnType.Date, 0).Should().BeEmpty("never 01-Jan-0001");
        row.Overdue.Should().Be(1);
        row.Given.Should().Be(0);
    }
}
