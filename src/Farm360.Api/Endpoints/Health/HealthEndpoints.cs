using Farm360.Application.Health.Commands.DiseaseIncidents;
using Farm360.Application.Health.Commands.MedicalTreatments;
using Farm360.Application.Health.Commands.MortalityRecords;
using Farm360.Application.Health.Commands.VaccinationEvents;
using Farm360.Application.Health.Commands.VaccinationProtocols;
using Farm360.Application.Health.Commands.VetVisits;
using Farm360.Application.Health.Queries.AnimalHealth;
using Farm360.Application.Health.Queries.Deworming;
using Farm360.Application.Health.Queries.DiseaseIncidents;
using Farm360.Application.Health.Queries.MedicalTreatments;
using Farm360.Application.Health.Queries.MortalityRecords;
using Farm360.Application.Health.Queries.SpecializedReports;
using Farm360.Application.Health.Queries.VaccinationEvents;
using Farm360.Application.Health.Queries.VaccinationProtocols;
using Farm360.Application.Health.Queries.VetVisits;
using Farm360.Application.Health.Queries.Dashboard;
using Farm360.Domain.Health.Enums;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/health")
            .WithTags("Health & Veterinary")
            .RequireAuthorization();

        // ── Dashboard ────────────────────────────────────────────────────────────

        group.MapGet("/dashboard", async (
            [FromQuery] Guid? farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetHealthDashboardQuery(farmId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get health dashboard summary stats");

        // ── Vaccination Protocols ────────────────────────────────────────────────

        group.MapPost("/protocols", async (
            [FromBody] CreateVaccinationProtocolCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Ok(new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Create a new vaccination protocol");

        group.MapGet("/protocols", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null,
            [FromServices] ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetVaccinationProtocolsQuery(pageNumber, pageSize, searchTerm), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get paginated list of vaccination protocols");

        group.MapGet("/protocols/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetVaccinationProtocolDetailQuery(id), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get details of a vaccination protocol");

        group.MapPut("/protocols/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateVaccinationProtocolCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route id must match command id.");
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Edit}")
        .WithSummary("Update an existing vaccination protocol");

        group.MapPost("/protocols/assign", async (
            [FromBody] AssignProtocolToAnimalsCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(command, ct);
            return Results.Ok();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Assign a vaccination protocol to a list of animals");

        // ── Vaccinations ─────────────────────────────────────────────────────────

        group.MapPost("/vaccinations/schedule", async (
            [FromBody] ScheduleVaccinationCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var eventId = await sender.Send(command, ct);
            return Results.Ok(new { Id = eventId });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Schedule a single vaccination for an animal");

        group.MapPut("/vaccinations/{id:guid}/administer", async (
            [FromRoute] Guid id,
            [FromBody] AdministerVaccinationRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var command = new RecordVaccinationAdministrationCommand(id, request.AdministeredDate, request.Notes);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Edit}")
        .WithSummary("Record administration of a scheduled vaccination");

        group.MapGet("/vaccinations/upcoming", async (
            [FromQuery] Guid farmId,
            [FromQuery] DateOnly beforeDate,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUpcomingVaccinationsQuery(farmId, beforeDate), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get upcoming scheduled vaccinations");

        // ── Medical Treatments ───────────────────────────────────────────────────

        group.MapPost("/treatments", async (
            [FromBody] LogMedicalTreatmentCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var treatmentId = await sender.Send(command, ct);
            return Results.Ok(new { Id = treatmentId });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Log a new medical treatment");

        group.MapGet("/treatments", async (
            [FromQuery] Guid? animalId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromServices] ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetTreatmentListQuery(animalId, pageNumber, pageSize), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get paginated list of medical treatments");

        group.MapPut("/treatments/{id:guid}/status", async (
            [FromRoute] Guid id,
            [FromBody] UpdateTreatmentStatusRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new UpdateTreatmentStatusCommand(id, request.Status, request.Notes), ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Edit}")
        .WithSummary("Update status of a medical treatment");

        // ── Disease Incidents ────────────────────────────────────────────────────

        group.MapPost("/incidents", async (
            [FromBody] ReportDiseaseIncidentCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var incidentId = await sender.Send(command, ct);
            return Results.Ok(new { Id = incidentId });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Report a new disease incident or outbreak");

        group.MapPut("/incidents/{id:guid}/status", async (
            [FromRoute] Guid id,
            [FromBody] UpdateIncidentStatusRequest request,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new UpdateDiseaseIncidentCommand(id, request.Status, request.AffectedAnimalCount, request.Notes), ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Edit}")
        .WithSummary("Update status and affected count of an incident");

        group.MapGet("/incidents", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromServices] ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetDiseaseIncidentListQuery(pageNumber, pageSize), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get paginated list of disease incidents");

        group.MapGet("/incidents/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDiseaseIncidentDetailQuery(id), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get details of a disease incident");

        // ── Mortality Records ────────────────────────────────────────────────────

        group.MapPost("/mortality", async (
            [FromBody] RecordMortalityCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Ok(new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Record an animal death");

        group.MapGet("/mortality", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromServices] ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetMortalityRecordsQuery(pageNumber, pageSize), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get paginated list of mortality records");

        // ── Vet Visits ───────────────────────────────────────────────────────────

        group.MapPost("/vet-visits", async (
            [FromBody] CreateVetVisitCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Ok(new { Id = id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Create}")
        .WithSummary("Log a vet visit");

        group.MapGet("/vet-visits", async (
            [FromQuery] Guid? farmId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromServices] ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetVetVisitListQuery(farmId, pageNumber, pageSize), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get paginated list of vet visits");

        group.MapGet("/vet-visits/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetVetVisitDetailQuery(id), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get details of a vet visit");

        group.MapPut("/vet-visits/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateVetVisitCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("ID mismatch");
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.Edit}")
        .WithSummary("Update a vet visit");

        // ── Animal Health History ────────────────────────────────────────────────

        group.MapGet("/animals/{animalId:guid}/history", async (
            [FromRoute] Guid animalId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAnimalHealthHistoryQuery(animalId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get complete health history for an animal");

        // ── Reports & Specialized Queries ────────────────────────────────────────

        group.MapGet("/reports/animals/{animalId:guid}", async (
            [FromRoute] Guid animalId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAnimalHealthReportQuery(animalId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get animal health report including incidents");

        group.MapGet("/deworming/calendar", async (
            [FromQuery] Guid farmId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromServices] ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var result = await sender.Send(new GetDewormingCalendarQuery(farmId, pageNumber, pageSize), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get deworming calendar events");

        group.MapGet("/reports/withdrawals", async (
            [FromQuery] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMilkWithdrawalAnimalsQuery(farmId), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.HealthModule.View}")
        .WithSummary("Get animals currently under milk/meat withdrawal periods");

        return app;
    }
}

public sealed record AdministerVaccinationRequest(DateOnly AdministeredDate, string? Notes);
public sealed record UpdateTreatmentStatusRequest(TreatmentStatus Status, string? Notes);
public sealed record UpdateIncidentStatusRequest(IncidentStatus Status, int AffectedAnimalCount, string? Notes);
