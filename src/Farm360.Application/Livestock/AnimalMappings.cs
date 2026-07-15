using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;

namespace Farm360.Application.Livestock;

/// <summary>
/// Extension methods mapping domain entities to DTOs.
/// Constitution §8 CQRS: No AutoMapper — explicit mappings are more refactor-safe
/// and avoid the "magic mapping" problem that hides breaking changes.
/// These are pure functions with no dependencies — fast and testable.
/// </summary>
public static class AnimalMappings
{
    public static AnimalDto ToDto(this Animal animal) =>
        new(
            Id: animal.Id,
            TenantId: animal.TenantId,
            FarmId: animal.FarmId,
            ShedId: animal.ShedId,
            TagId: animal.Tag.TagId,
            TagType: animal.Tag.TagType,
            Species: animal.Species,
            BreedName: animal.BreedName,
            Sex: animal.Sex,
            DateOfBirth: animal.DateOfBirth,
            AcquisitionType: animal.AcquisitionType,
            AcquisitionDate: animal.AcquisitionDate,
            AcquisitionPriceBdt: animal.AcquisitionPriceBdt,
            SalePriceBdt: animal.SalePriceBdt,
            SaleDate: animal.SaleDate,
            Status: animal.Status,
            QuarantineReason: animal.QuarantineReason,
            DisposalReason: animal.DisposalReason,
            Notes: animal.Notes,
            LatestWeightKg: animal.LatestWeightKg,
            LatestWeightDate: animal.LatestWeightDate,
            AdgKgPerDay: animal.AdgKgPerDay,
            PrimaryPhotoUrl: animal.Photos.FirstOrDefault(p => p.IsPrimary)?.PhotoUrl,
            WeightRecords: animal.WeightRecords.Select(w => w.ToDto()).ToList().AsReadOnly(),
            BreedingRecords: animal.BreedingRecords.Select(b => b.ToDto()).ToList().AsReadOnly(),
            Photos: animal.Photos.Select(p => p.ToDto()).ToList().AsReadOnly(),
            CreatedAtUtc: animal.CreatedAtUtc,
            CreatedBy: animal.CreatedBy,
            ModifiedAtUtc: animal.ModifiedAtUtc);

    public static AnimalListItemDto ToListItemDto(this Animal animal) =>
        new(
            Id: animal.Id,
            TagId: animal.Tag.TagId,
            TagType: animal.Tag.TagType,
            Species: animal.Species,
            BreedName: animal.BreedName,
            Sex: animal.Sex,
            DateOfBirth: animal.DateOfBirth,
            Status: animal.Status,
            FarmId: animal.FarmId,
            ShedId: animal.ShedId,
            LatestWeightKg: animal.LatestWeightKg,
            LatestWeightDate: animal.LatestWeightDate,
            AdgKgPerDay: animal.AdgKgPerDay,
            PrimaryPhotoUrl: animal.Photos.FirstOrDefault(p => p.IsPrimary)?.PhotoUrl,
            CreatedAtUtc: animal.CreatedAtUtc);

    public static WeightRecordDto ToDto(this WeightRecord record) =>
        new(
            Id: record.Id,
            AnimalId: record.AnimalId,
            WeightKg: record.Weight.WeightKg,
            RecordedDate: record.RecordedDate,
            Notes: record.Notes,
            RecordedAtUtc: record.RecordedAtUtc);

    public static BreedingRecordDto ToDto(this BreedingRecord record) =>
        new(
            Id: record.Id,
            AnimalId: record.AnimalId,
            MatingDate: record.MatingDate,
            SireAnimalId: record.SireAnimalId,
            SireExternalId: record.SireExternalId,
            IsArtificialInsemination: record.IsArtificialInsemination,
            PregnancyConfirmDate: record.PregnancyConfirmDate,
            IsPregnancyConfirmed: record.IsPregnancyConfirmed,
            ExpectedCalvingDate: record.ExpectedCalvingDate,
            ActualCalvingDate: record.ActualCalvingDate,
            CalvingOutcome: record.CalvingOutcome,
            CalvesCount: record.CalvesCount,
            CreatedAtUtc: record.CreatedAtUtc);

    public static AnimalPhotoDto ToDto(this AnimalPhoto photo) =>
        new(
            Id: photo.Id,
            AnimalId: photo.AnimalId,
            PhotoUrl: photo.PhotoUrl,
            Caption: photo.Caption,
            IsPrimary: photo.IsPrimary,
            UploadedAtUtc: photo.UploadedAtUtc);
}
