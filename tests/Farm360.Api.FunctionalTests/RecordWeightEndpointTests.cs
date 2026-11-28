using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Farm360.Api.Endpoints.Livestock;
using Farm360.Domain.Livestock.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>POST /livestock/animals/{id}/weights takes a scale kg or a tape measure (length + girth).</summary>
public class RecordWeightEndpointTests
{
    private const string Route = "POST /api/v1/livestock/animals/{id:guid}/weights";

    // Same settings as Program.ConfigureHttpJsonOptions: web defaults with enums as strings.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void TapeBodyBindsWithoutAKg()
    {
        var body = JsonSerializer.Deserialize<RecordWeightRequest>(
            """{"recordedDate":"2025-06-01","method":"Tape","lengthInch":62,"girthInch":51}""", Json)!;

        body.Method.Should().Be(WeightMethod.Tape);
        body.WeightKg.Should().BeNull();
        body.LengthInch.Should().Be(62m);
        body.GirthInch.Should().Be(51m);
    }

    [Fact]
    public void ScaleBodyFromOlderClientsStillBinds()
    {
        var body = JsonSerializer.Deserialize<RecordWeightRequest>(
            """{"weightKg":250.5,"recordedDate":"2025-06-01","notes":"x"}""", Json)!;

        body.WeightKg.Should().Be(250.5m);
        body.Method.Should().BeNull();
        body.LengthInch.Should().BeNull();
        body.RecordedDate.Should().Be(new DateOnly(2025, 6, 1));
    }

    [Fact]
    public void StillRequiresAnimalWritePermission()
    {
        RolePolicy.PolicyOf(Route).Should().NotBeNullOrEmpty();
    }
}
