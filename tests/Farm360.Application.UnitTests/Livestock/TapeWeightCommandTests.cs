using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Localization;
using Farm360.Application.Livestock.Commands;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Livestock;

/// <summary>
/// Recording a weight from a tape measure: the command accepts a kg or a length + girth, and with
/// Tape the server computes the kg itself (L × G² ÷ 660) and stores the measurements.
/// </summary>
public sealed class TapeWeightCommandTests
{
    private readonly IAnimalRepository _repo = Substitute.For<IAnimalRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private static readonly RecordWeightCommandValidator Validator = new();
    private static readonly DateOnly Day = new(2025, 6, 1);

    private RecordWeightCommandHandler Handler() => new(_repo, _uow, _currentUser, _publisher);

    private Animal Stub(string tag = "T-001")
    {
        var animal = Animal.Create(Guid.NewGuid(), Guid.NewGuid(), AnimalTag.Create(tag, TagType.EarTag),
            AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male, new DateOnly(2023, 1, 1),
            AcquisitionType.Purchased, new DateOnly(2024, 1, 1), 50_000m, null);
        _repo.GetByIdWithWeightsAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        return animal;
    }

    // ── Handler ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tape_ComputesKgAndStoresMeasurements()
    {
        var animal = Stub();

        var dto = await Handler().Handle(
            new RecordWeightCommand(animal.Id, null, Day, null, WeightMethod.Tape, 62m, 51m), CancellationToken.None);

        dto.WeightKg.Should().Be(244.3m);
        dto.Method.Should().Be(WeightMethod.Tape);
        dto.LengthInch.Should().Be(62m);
        dto.GirthInch.Should().Be(51m);
        var record = animal.WeightRecords.Should().ContainSingle().Subject;
        record.Method.Should().Be(WeightMethod.Tape);
        record.LengthInch.Should().Be(62m);
        record.GirthInch.Should().Be(51m);
        record.Weight.WeightKg.Should().Be(244.3m);
        animal.LatestWeightKg.Should().Be(244.3m);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tape_IgnoresAKgSentByTheClient()
    {
        var animal = Stub();

        var dto = await Handler().Handle(
            new RecordWeightCommand(animal.Id, 999m, Day, null, WeightMethod.Tape, 62m, 51m), CancellationToken.None);

        dto.WeightKg.Should().Be(244.3m);
        animal.WeightRecords.Single().Weight.WeightKg.Should().Be(244.3m);
    }

    [Fact]
    public async Task MethodOmitted_WithOnlyMeasurements_IsTape()
    {
        var animal = Stub();

        var dto = await Handler().Handle(
            new RecordWeightCommand(animal.Id, null, Day, null, null, 62m, 51m), CancellationToken.None);

        dto.Method.Should().Be(WeightMethod.Tape);
        dto.WeightKg.Should().Be(244.3m);
    }

    [Fact]
    public async Task Scale_StoresNoMeasurements_EvenIfSent()
    {
        var animal = Stub();

        var dto = await Handler().Handle(
            new RecordWeightCommand(animal.Id, 250m, Day, null, WeightMethod.Scale, 62m, 51m), CancellationToken.None);

        dto.WeightKg.Should().Be(250m);
        dto.Method.Should().Be(WeightMethod.Scale);
        dto.LengthInch.Should().BeNull();
        dto.GirthInch.Should().BeNull();
    }

    [Fact]
    public async Task Batch_EachAnimalGetsItsOwnTapeEstimate()
    {
        // The batch dialog (and the list's bulk "Record Weights") sends one command per animal.
        var a = Stub("T-001");
        var b = Stub("T-002");
        var c = Stub("T-003");

        await Handler().Handle(new RecordWeightCommand(a.Id, null, Day, null, WeightMethod.Tape, 62m, 51m), CancellationToken.None);
        await Handler().Handle(new RecordWeightCommand(b.Id, null, Day, null, WeightMethod.Tape, 60m, 60m), CancellationToken.None);
        await Handler().Handle(new RecordWeightCommand(c.Id, 300m, Day, null, WeightMethod.Scale), CancellationToken.None);

        a.WeightRecords.Single().Weight.WeightKg.Should().Be(244.3m);
        b.WeightRecords.Single().Weight.WeightKg.Should().Be(327.3m);
        b.WeightRecords.Single().GirthInch.Should().Be(60m);
        c.WeightRecords.Single().Method.Should().Be(WeightMethod.Scale);
        await _uow.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Validator ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validator_TapeWithMeasurements_Passes()
    {
        Validator.TestValidate(new RecordWeightCommand(Guid.NewGuid(), null, Day, null, WeightMethod.Tape, 62m, 51m))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_Tape_DoesNotRequireOrCheckKg()
    {
        Validator.TestValidate(new RecordWeightCommand(Guid.NewGuid(), -1m, Day, null, WeightMethod.Tape, 62m, 51m))
            .ShouldNotHaveValidationErrorFor(x => x.WeightKg);
    }

    [Fact]
    public void Validator_TapeMissingMeasurements_Fails()
    {
        var result = Validator.TestValidate(new RecordWeightCommand(Guid.NewGuid(), null, Day, null, WeightMethod.Tape));

        result.ShouldHaveValidationErrorFor(x => x.LengthInch);
        result.ShouldHaveValidationErrorFor(x => x.GirthInch);
    }

    [Theory]
    [InlineData(19.9, 51, "LengthInch", TapeWeight.LengthOutOfRangeMessage)]
    [InlineData(121, 51, "LengthInch", TapeWeight.LengthOutOfRangeMessage)]
    [InlineData(62, 19, "GirthInch", TapeWeight.GirthOutOfRangeMessage)]
    [InlineData(62, 130, "GirthInch", TapeWeight.GirthOutOfRangeMessage)]
    public void Validator_TapeOutOfRange_Fails(decimal length, decimal girth, string property, string message)
    {
        var result = Validator.TestValidate(new RecordWeightCommand(Guid.NewGuid(), null, Day, null, WeightMethod.Tape, length, girth));

        result.ShouldHaveValidationErrorFor(property).WithErrorMessage(message);
    }

    [Fact]
    public void Validator_TapeEstimateOver2000Kg_Fails()
    {
        var result = Validator.TestValidate(new RecordWeightCommand(Guid.NewGuid(), null, Day, null, WeightMethod.Tape, 120m, 120m));

        result.ShouldHaveAnyValidationError().WithErrorMessage(TapeWeight.EstimateTooHighMessage);
    }

    [Fact]
    public void Validator_ScaleWithoutKg_Fails()
    {
        Validator.TestValidate(new RecordWeightCommand(Guid.NewGuid(), null, Day, null, WeightMethod.Scale))
            .ShouldHaveValidationErrorFor(x => x.WeightKg);
    }

    [Theory]
    [InlineData(TapeWeight.LengthOutOfRangeMessage)]
    [InlineData(TapeWeight.GirthOutOfRangeMessage)]
    [InlineData(TapeWeight.EstimateTooHighMessage)]
    [InlineData("Enter the body length for a tape-measure weight.")]
    [InlineData("Enter the heart girth for a tape-measure weight.")]
    [InlineData("Enter the weight in kg.")]
    public void EveryTapeMessage_HasBangla(string english)
    {
        BanglaMessageCatalogue.Templates.Should().ContainKey(english);
    }
}
