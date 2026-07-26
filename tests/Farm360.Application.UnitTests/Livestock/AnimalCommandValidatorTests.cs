using Farm360.Application.Livestock.Commands;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Livestock;

/// <summary>
/// Unit tests for livestock command validators.
/// Validators are pure functions — no DB, no DI beyond the repository stub.
/// Uses FluentValidation.TestHelper for clean assertion DSL.
/// </summary>
public sealed class AnimalCommandValidatorTests
{
    // ── RegisterAnimalCommandValidator ────────────────────────────────────────

    private readonly IAnimalRepository _repo = Substitute.For<IAnimalRepository>();
    private readonly RegisterAnimalCommandValidator _registerValidator;

    public AnimalCommandValidatorTests()
    {
        // Default: tag does NOT exist (uniqueness passes)
        _repo.TagExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
             .Returns(false);
        _registerValidator = new RegisterAnimalCommandValidator(_repo);
    }

    private static RegisterAnimalCommand ValidRegisterCommand() => new(
        FarmId:              Guid.NewGuid(),
        TagId:               "B-001",
        TagType:             TagType.EarTag,
        Species:             AnimalSpecies.CattleBeef,
        BreedName:           "Shahiwal",
        Sex:                 AnimalSex.Male,
        DateOfBirth:         new DateOnly(2023, 1, 1),
        AcquisitionType:     AcquisitionType.Purchased,
        AcquisitionDate:     new DateOnly(2024, 1, 1),
        AcquisitionPriceBdt: 50_000m,
        Notes:               null);

    [Fact]
    public async Task RegisterAnimal_ValidCommand_PassesValidation()
    {
        var result = await _registerValidator.TestValidateAsync(ValidRegisterCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task RegisterAnimal_EmptyFarmId_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { FarmId = Guid.Empty };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.FarmId);
    }

    [Fact]
    public async Task RegisterAnimal_EmptyTagId_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { TagId = string.Empty };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.TagId);
    }

    [Fact]
    public async Task RegisterAnimal_TagIdOver50Chars_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { TagId = new string('X', 51) };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.TagId);
    }

    [Fact]
    public async Task RegisterAnimal_DuplicateTagId_FailsValidation()
    {
        _repo.TagExistsAsync("B-001", null, Arg.Any<CancellationToken>()).Returns(true);
        var cmd = ValidRegisterCommand() with { TagId = "B-001" };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.TagId);
    }

    [Fact]
    public async Task RegisterAnimal_FutureDateOfBirth_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public async Task RegisterAnimal_AcquisitionBeforeDob_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with
        {
            DateOfBirth     = new DateOnly(2024, 6, 1),
            AcquisitionDate = new DateOnly(2024, 1, 1), // before DOB
        };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.AcquisitionDate);
    }

    [Fact]
    public async Task RegisterAnimal_NegativeAcquisitionPrice_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { AcquisitionPriceBdt = -1m };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.AcquisitionPriceBdt);
    }

    [Fact]
    public async Task RegisterAnimal_EmptyBreedName_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { BreedName = string.Empty };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.BreedName);
    }

    [Fact]
    public async Task RegisterAnimal_NotesOver2000Chars_FailsValidation()
    {
        var cmd = ValidRegisterCommand() with { Notes = new string('X', 2001) };

        var result = await _registerValidator.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    // ── RecordWeightCommandValidator ──────────────────────────────────────────

    private static readonly RecordWeightCommandValidator WeightValidator = new();

    private static RecordWeightCommand ValidWeightCommand() => new(
        AnimalId:     Guid.NewGuid(),
        WeightKg:     250m,
        RecordedDate: DateOnly.FromDateTime(DateTime.UtcNow),
        Notes:        null);

    [Fact]
    public void RecordWeight_ValidCommand_PassesValidation()
    {
        var result = WeightValidator.TestValidate(ValidWeightCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RecordWeight_ZeroWeight_FailsValidation()
    {
        var cmd = ValidWeightCommand() with { WeightKg = 0m };

        var result = WeightValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.WeightKg);
    }

    [Fact]
    public void RecordWeight_WeightOver2000_FailsValidation()
    {
        var cmd = ValidWeightCommand() with { WeightKg = 2001m };

        var result = WeightValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.WeightKg);
    }

    [Fact]
    public void RecordWeight_FutureDate_FailsValidation()
    {
        var cmd = ValidWeightCommand() with { RecordedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };

        var result = WeightValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.RecordedDate);
    }

    [Fact]
    public void RecordWeight_NullAnimalId_FailsValidation()
    {
        var cmd = ValidWeightCommand() with { AnimalId = Guid.Empty };

        var result = WeightValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.AnimalId);
    }

    // ── SellAnimalCommandValidator ────────────────────────────────────────────

    private static readonly SellAnimalCommandValidator SellValidator = new();

    private static SellAnimalCommand ValidSellCommand() => new(
        AnimalId:    Guid.NewGuid(),
        SalePriceBdt: 75_000m,
        SaleDate:    DateOnly.FromDateTime(DateTime.UtcNow),
        BuyerName:   null,
        SaleWeightKg: null);

    [Fact]
    public void SellAnimal_ValidCommand_PassesValidation()
    {
        var result = SellValidator.TestValidate(ValidSellCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SellAnimal_ZeroPrice_FailsValidation()
    {
        var cmd = ValidSellCommand() with { SalePriceBdt = 0m };

        var result = SellValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.SalePriceBdt);
    }

    [Fact]
    public void SellAnimal_FutureSaleDate_FailsValidation()
    {
        var cmd = ValidSellCommand() with { SaleDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };

        var result = SellValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.SaleDate);
    }

    // ── QuarantineAnimalCommandValidator ─────────────────────────────────────

    private static readonly QuarantineAnimalCommandValidator QuarantineValidator = new();

    [Fact]
    public void QuarantineAnimal_ValidCommand_PassesValidation()
    {
        var cmd    = new QuarantineAnimalCommand(Guid.NewGuid(), "Suspected FMD");
        var result = QuarantineValidator.TestValidate(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void QuarantineAnimal_EmptyReason_FailsValidation()
    {
        var cmd    = new QuarantineAnimalCommand(Guid.NewGuid(), string.Empty);
        var result = QuarantineValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void QuarantineAnimal_ReasonOver500Chars_FailsValidation()
    {
        var cmd    = new QuarantineAnimalCommand(Guid.NewGuid(), new string('X', 501));
        var result = QuarantineValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Validator_WhenSalePriceIsZero_ShouldHaveError()
    {
        var command = new SellAnimalCommand(Guid.NewGuid(), 0m, new DateOnly(2025, 7, 1), null, null);
        var result = SellValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SalePriceBdt);
    }
}
