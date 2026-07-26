using Farm360.Application.Livestock.Commands;
using Farm360.Application.Livestock.DTOs;
using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Livestock.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Livestock;

/// <summary>
/// Livestock module — Minimal API endpoints.
/// Constitution §6 (API Standards): Minimal API, no controllers, route groups.
/// F360-AUTH-2026-001 §7.3: All endpoints require permission-based authorization.
///
/// Route prefix: /api/v1/livestock  (registered in Program.cs)
/// All endpoints require JWT authentication (inherited from RequireAuthorization on group).
/// Permissions are checked by PermissionHandler → IPermissionService → Redis-cached.
/// </summary>
public static class LivestockEndpoints
{
    public static RouteGroupBuilder MapLivestockEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Livestock");
        group.RequireAuthorization();

        // ── Animal CRUD ────────────────────────────────────────────────────────
        group.MapGet("/animals", GetAnimalList)
            .WithName("GetAnimalList")
            .WithSummary("Get paginated list of animals")
            .WithDescription("Supports filtering by farm, shed, species, sex, status, and free-text search. Sorted and paginated.")
            .Produces<PagedAnimalListDto>()
            .RequireAuthorization("Permission:animals.view");

        group.MapGet("/animals/{id:guid}", GetAnimalById)
            .WithName("GetAnimalById")
            .WithSummary("Get full animal detail including weight records, breeding records, and photos")
            .Produces<AnimalDto>()
            .Produces(404)
            .RequireAuthorization("Permission:animals.view");

        group.MapPost("/animals", RegisterAnimal)
            .WithName("RegisterAnimal")
            .WithSummary("Register a new animal")
            .Produces<AnimalDto>(201)
            .Produces(422)
            .RequireAuthorization("Permission:animals.create");

        group.MapDelete("/animals/{id:guid}", DeleteAnimal)
            .WithName("DeleteAnimal")
            .WithSummary("Soft-delete an animal (IsDeleted = true, data retained)")
            .Produces(204)
            .Produces(404)
            .RequireAuthorization("Permission:animals.delete");

        // ── Weight Records ─────────────────────────────────────────────────────
        group.MapGet("/animals/{id:guid}/weights", GetWeightHistory)
            .WithName("GetAnimalWeightHistory")
            .WithSummary("Get chronological weight history for an animal")
            .Produces<IReadOnlyList<WeightRecordDto>>()
            .Produces(404)
            .RequireAuthorization("Permission:animals.view");

        group.MapPost("/animals/{id:guid}/weights", RecordWeight)
            .WithName("RecordAnimalWeight")
            .WithSummary("Record a new weight measurement")
            .Produces<WeightRecordDto>(201)
            .Produces(422)
            .RequireAuthorization("Permission:animals.create");

        // ── Status Transitions ─────────────────────────────────────────────────
        group.MapPost("/animals/{id:guid}/sell", SellAnimal)
            .WithName("SellAnimal")
            .WithSummary("Record animal sale — transitions status to Sold")
            .Produces(204)
            .Produces(404)
            .Produces(422)
            .RequireAuthorization("Permission:animals.sell");

        group.MapPost("/animals/{id:guid}/quarantine", QuarantineAnimal)
            .WithName("QuarantineAnimal")
            .WithSummary("Place animal under quarantine")
            .Produces(204)
            .Produces(404)
            .Produces(422)
            .RequireAuthorization("Permission:animals.quarantine");

        group.MapPost("/animals/{id:guid}/release-quarantine", ReleaseFromQuarantine)
            .WithName("ReleaseAnimalFromQuarantine")
            .WithSummary("Release animal from quarantine back to Active")
            .Produces(204)
            .Produces(404)
            .Produces(422)
            .RequireAuthorization("Permission:animals.quarantine");

        group.MapPost("/animals/{id:guid}/death", RecordDeath)
            .WithName("RecordAnimalDeath")
            .WithSummary("Record animal death with cause")
            .Produces(204)
            .Produces(404)
            .Produces(422)
            .RequireAuthorization("Permission:animals.edit");

        group.MapPost("/animals/{id:guid}/transfer", TransferAnimal)
            .WithName("TransferAnimal")
            .WithSummary("Transfer animal to a different location (Shed/Pen)")
            .Produces(204)
            .Produces(404)
            .Produces(422)
            .RequireAuthorization("Permission:animals.edit");

        // ── Photos ─────────────────────────────────────────────────────────────
        group.MapPost("/animals/{id:guid}/photos", AddPhoto)
            .WithName("AddAnimalPhoto")
            .WithSummary("Register an S3 photo URL on the animal after client upload")
            .Produces<AnimalPhotoDto>(201)
            .Produces(422)
            .RequireAuthorization("Permission:animals.create");

        // ── Breeding ───────────────────────────────────────────────────────────
        group.MapPost("/animals/{id:guid}/breeding", RecordMating)
            .WithName("RecordMating")
            .WithSummary("Record a mating event")
            .Produces(204)
            .Produces(422)
            .RequireAuthorization("Permission:animals.edit");

        group.MapPut("/animals/{id:guid}/breeding/{recordId:guid}/pregnancy", ConfirmPregnancy)
            .WithName("ConfirmPregnancy")
            .WithSummary("Confirm pregnancy for a breeding record")
            .Produces(204)
            .Produces(422)
            .RequireAuthorization("Permission:animals.edit");

        group.MapPut("/animals/{id:guid}/breeding/{recordId:guid}/calving", RecordCalving)
            .WithName("RecordCalving")
            .WithSummary("Record calving outcome")
            .Produces(204)
            .Produces(422)
            .RequireAuthorization("Permission:animals.edit");

        return group;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HANDLERS
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task<IResult> GetAnimalList(
        ISender sender,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? farmId = null,
        [FromQuery] Guid? shedId = null,
        [FromQuery] Guid? penId = null,
        [FromQuery] AnimalSpecies? species = null,
        [FromQuery] AnimalSex? sex = null,
        [FromQuery] AnimalStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetAnimalListQuery(pageNumber, pageSize, farmId, shedId, penId, species, sex, status, search, sortBy, sortDesc),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAnimalById(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var animal = await sender.Send(new GetAnimalByIdQuery(id), cancellationToken);
        return animal is null ? Results.NotFound() : Results.Ok(animal);
    }

    private static async Task<IResult> RegisterAnimal(
        RegisterAnimalCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Created($"/api/v1/livestock/animals/{result.Id}", result);
    }

    private static async Task<IResult> DeleteAnimal(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteAnimalCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetWeightHistory(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var records = await sender.Send(new GetAnimalWeightHistoryQuery(id), cancellationToken);
        return Results.Ok(records);
    }

    private static async Task<IResult> RecordWeight(
        Guid id,
        [FromBody] RecordWeightRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RecordWeightCommand(id, request.WeightKg, request.RecordedDate, request.Notes),
            cancellationToken);
        return Results.Created($"/api/v1/livestock/animals/{id}/weights/{result.Id}", result);
    }

    private static async Task<IResult> SellAnimal(
        Guid id,
        [FromBody] SellAnimalRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SellAnimalCommand(id, request.SalePriceBdt, request.SaleDate, request.BuyerName, request.SaleWeightKg), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> QuarantineAnimal(
        Guid id,
        [FromBody] QuarantineAnimalRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new QuarantineAnimalCommand(id, request.Reason), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ReleaseFromQuarantine(
        Guid id, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new ReleaseFromQuarantineCommand(id), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RecordDeath(
        Guid id,
        [FromBody] RecordDeathRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new RecordAnimalDeathCommand(id, request.Cause, request.DeathDate, request.Notes),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> TransferAnimal(
        Guid id,
        [FromBody] TransferAnimalRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new TransferAnimalCommand(id, request.ToShedId, request.ToPenId, request.TransferDate, request.Reason),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddPhoto(
        Guid id,
        [FromBody] AddPhotoRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddAnimalPhotoCommand(id, request.PhotoUrl, request.Caption),
            cancellationToken);
        return Results.Created($"/api/v1/livestock/animals/{id}/photos/{result.Id}", result);
    }

    private static async Task<IResult> RecordMating(
        Guid id,
        [FromBody] RecordMatingRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new RecordMatingCommand(id, request.MatingDate, request.SireAnimalId, request.SireExternalId, request.IsArtificialInsemination), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmPregnancy(
        Guid id,
        Guid recordId,
        [FromBody] ConfirmPregnancyRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ConfirmPregnancyCommand(id, recordId, request.ConfirmDate, request.ExpectedCalvingDate), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RecordCalving(
        Guid id,
        Guid recordId,
        [FromBody] RecordCalvingRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await sender.Send(new RecordCalvingCommand(id, recordId, request.CalvingDate, request.Outcome, request.CalvesCount), cancellationToken);
        return Results.NoContent();
    }
}

// ── Request bodies (simple records — not commands, to decouple route param binding) ──

/// <summary>Weight recording request body.</summary>
public sealed record RecordWeightRequest(decimal WeightKg, DateOnly RecordedDate, string? Notes);

/// <summary>Sale request body.</summary>
public sealed record SellAnimalRequest(decimal SalePriceBdt, DateOnly SaleDate, string? BuyerName, decimal? SaleWeightKg);

/// <summary>Quarantine request body.</summary>
public sealed record QuarantineAnimalRequest(string Reason);

/// <summary>Death recording request body.</summary>
public sealed record RecordDeathRequest(DisposalReason Cause, DateOnly DeathDate, string? Notes);

/// <summary>Location transfer request body.</summary>
public sealed record TransferAnimalRequest(Guid? ToShedId, Guid? ToPenId, DateOnly TransferDate, string? Reason);

/// <summary>Photo registration request body (URL from S3 upload).</summary>
public sealed record AddPhotoRequest(string PhotoUrl, string? Caption);

public sealed record RecordMatingRequest(DateOnly MatingDate, Guid? SireAnimalId, string? SireExternalId, bool IsArtificialInsemination);
public sealed record ConfirmPregnancyRequest(DateOnly ConfirmDate, DateOnly ExpectedCalvingDate);
public sealed record RecordCalvingRequest(DateOnly CalvingDate, string Outcome, int CalvesCount);
