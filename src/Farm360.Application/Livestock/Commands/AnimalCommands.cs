using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

// ══════════════════════════════════════════════════════════════════════════════
// REGISTER ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Registers a new animal. Creates the Animal aggregate and raises AnimalRegisteredEvent.
/// Permission: animals:write
/// </summary>
public sealed record RegisterAnimalCommand(
    Guid FarmId,
    Guid? ShedId,
    string TagId,
    TagType TagType,
    AnimalSpecies Species,
    string BreedName,
    AnimalSex Sex,
    DateOnly DateOfBirth,
    AcquisitionType AcquisitionType,
    DateOnly AcquisitionDate,
    decimal? AcquisitionPriceBdt,
    string? Notes) : IRequest<AnimalDto>;

public sealed class RegisterAnimalCommandValidator : AbstractValidator<RegisterAnimalCommand>
{
    public RegisterAnimalCommandValidator(IAnimalRepository repository)
    {
        RuleFor(x => x.FarmId)
            .NotEmpty().WithMessage("Farm is required.");

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("Tag ID is required.")
            .MaximumLength(50).WithMessage("Tag ID cannot exceed 50 characters.");

        // Async uniqueness check — uses IAnimalRepository (tenant-scoped by Global Query Filter)
        RuleFor(x => x.TagId)
            .MustAsync(async (tagId, ct) => !await repository.TagExistsAsync(tagId, null, ct))
            .WithMessage("An animal with this Tag ID already exists in your farm.");

        RuleFor(x => x.BreedName)
            .NotEmpty().WithMessage("Breed name is required.")
            .MaximumLength(100).WithMessage("Breed name cannot exceed 100 characters.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.AcquisitionDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Acquisition date cannot be in the future.")
            .GreaterThanOrEqualTo(x => x.DateOfBirth)
            .WithMessage("Acquisition date cannot be before date of birth.");

        RuleFor(x => x.AcquisitionPriceBdt)
            .GreaterThan(0).WithMessage("Acquisition price must be greater than zero.")
            .When(x => x.AcquisitionPriceBdt.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}

public sealed class RegisterAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ITenantService tenantService) : IRequestHandler<RegisterAnimalCommand, AnimalDto>
{
    public async Task<AnimalDto> Handle(RegisterAnimalCommand request, CancellationToken cancellationToken)
    {
        var tag = AnimalTag.Create(request.TagId, request.TagType);

        var animal = Animal.Create(
            tenantId: tenantService.TenantId,
            farmId: request.FarmId,
            shedId: request.ShedId,
            tag: tag,
            species: request.Species,
            breedName: request.BreedName,
            sex: request.Sex,
            dateOfBirth: request.DateOfBirth,
            acquisitionType: request.AcquisitionType,
            acquisitionDate: request.AcquisitionDate,
            acquisitionPriceBdt: request.AcquisitionPriceBdt,
            notes: request.Notes);

        repository.Add(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return animal.ToDto();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RECORD WEIGHT
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Records a new weight measurement for an animal.
/// Permission: animals:write
/// </summary>
public sealed record RecordWeightCommand(
    Guid AnimalId,
    decimal WeightKg,
    DateOnly RecordedDate,
    string? Notes) : IRequest<WeightRecordDto>;

public sealed class RecordWeightCommandValidator : AbstractValidator<RecordWeightCommand>
{
    public RecordWeightCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();

        RuleFor(x => x.WeightKg)
            .GreaterThan(0).WithMessage("Weight must be greater than zero.")
            .LessThanOrEqualTo(2000).WithMessage("Weight cannot exceed 2000 kg.");

        RuleFor(x => x.RecordedDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Recorded date cannot be in the future.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public sealed class RecordWeightCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<RecordWeightCommand, WeightRecordDto>
{
    public async Task<WeightRecordDto> Handle(RecordWeightCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdWithWeightsAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var weight = Weight.Create(request.WeightKg);
        var record = animal.RecordWeight(weight, request.RecordedDate, currentUser.UserId ?? Guid.Empty, request.Notes);

        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return record.ToDto();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// SELL ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Sells the animal. Transitions status to Sold. Raises AnimalSoldEvent.
/// Permission: animals:sell
/// </summary>
public sealed record SellAnimalCommand(
    Guid AnimalId,
    decimal SalePriceBdt,
    DateOnly SaleDate) : IRequest;

public sealed class SellAnimalCommandValidator : AbstractValidator<SellAnimalCommand>
{
    public SellAnimalCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();

        RuleFor(x => x.SalePriceBdt)
            .GreaterThan(0).WithMessage("Sale price must be greater than zero.");

        RuleFor(x => x.SaleDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Sale date cannot be in the future.");
    }
}

public sealed class SellAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<SellAnimalCommand>
{
    public async Task Handle(SellAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.Sell(request.SalePriceBdt, request.SaleDate, currentUser.UserId ?? Guid.Empty);

        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// QUARANTINE ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Places animal under quarantine. Permission: animals:quarantine
/// </summary>
public sealed record QuarantineAnimalCommand(
    Guid AnimalId,
    string Reason) : IRequest;

public sealed class QuarantineAnimalCommandValidator : AbstractValidator<QuarantineAnimalCommand>
{
    public QuarantineAnimalCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Quarantine reason is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}

public sealed class QuarantineAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<QuarantineAnimalCommand>
{
    public async Task Handle(QuarantineAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.Quarantine(request.Reason);
        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RELEASE FROM QUARANTINE
// ══════════════════════════════════════════════════════════════════════════════

public sealed record ReleaseFromQuarantineCommand(Guid AnimalId) : IRequest;

public sealed class ReleaseFromQuarantineCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<ReleaseFromQuarantineCommand>
{
    public async Task Handle(ReleaseFromQuarantineCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.ReleaseFromQuarantine();
        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RECORD DEATH
// ══════════════════════════════════════════════════════════════════════════════

public sealed record RecordAnimalDeathCommand(
    Guid AnimalId,
    DisposalReason Cause,
    DateOnly DeathDate,
    string? Notes) : IRequest;

public sealed class RecordAnimalDeathCommandValidator : AbstractValidator<RecordAnimalDeathCommand>
{
    public RecordAnimalDeathCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.DeathDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Death date cannot be in the future.");
    }
}

public sealed class RecordAnimalDeathCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordAnimalDeathCommand>
{
    public async Task Handle(RecordAnimalDeathCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.RecordDeath(request.Cause, request.DeathDate, request.Notes);
        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TRANSFER ANIMAL TO SHED
// ══════════════════════════════════════════════════════════════════════════════

public sealed record TransferAnimalToShedCommand(
    Guid AnimalId,
    Guid? ToShedId,
    DateOnly TransferDate) : IRequest;

public sealed class TransferAnimalToShedCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<TransferAnimalToShedCommand>
{
    public async Task Handle(TransferAnimalToShedCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.TransferToShed(request.ToShedId, request.TransferDate);
        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DELETE ANIMAL (soft delete)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Soft-deletes an animal. Constitution §12: No hard deletes.
/// Permission: animals:delete
/// </summary>
public sealed record DeleteAnimalCommand(Guid AnimalId) : IRequest;

public sealed class DeleteAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<DeleteAnimalCommand>
{
    public async Task Handle(DeleteAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        repository.SoftDelete(animal, currentUser.UserId ?? Guid.Empty);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ADD PHOTO
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Adds a photo URL to the animal after the client has uploaded to S3.
/// The API returns a presigned upload URL; the client uploads directly; then calls this command.
/// Permission: animals:write
/// </summary>
public sealed record AddAnimalPhotoCommand(
    Guid AnimalId,
    string PhotoUrl,
    string? Caption) : IRequest<AnimalPhotoDto>;

public sealed class AddAnimalPhotoCommandValidator : AbstractValidator<AddAnimalPhotoCommand>
{
    public AddAnimalPhotoCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.PhotoUrl)
            .NotEmpty().WithMessage("Photo URL is required.")
            .MaximumLength(1000).WithMessage("Photo URL cannot exceed 1000 characters.");
        RuleFor(x => x.Caption)
            .MaximumLength(200)
            .When(x => x.Caption is not null);
    }
}

public sealed class AddAnimalPhotoCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<AddAnimalPhotoCommand, AnimalPhotoDto>
{
    public async Task<AnimalPhotoDto> Handle(AddAnimalPhotoCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var photo = animal.AddPhoto(request.PhotoUrl, request.Caption, currentUser.UserId ?? Guid.Empty);
        repository.Update(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return photo.ToDto();
    }
}
