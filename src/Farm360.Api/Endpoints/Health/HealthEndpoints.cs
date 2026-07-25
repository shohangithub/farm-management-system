using Farm360.Application.Health.Commands.DiseaseIncidents;
using Farm360.Application.Health.Commands.MedicalTreatments;
using Farm360.Application.Health.Commands.VaccinationEvents;
using Farm360.Application.Health.Queries.AnimalHealth;
using Farm360.Application.Health.Queries.VaccinationEvents;
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
        .WithSummary("Schedule a vaccination for an animal");

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
        .WithSummary("Get complete health history (vaccinations and treatments) for an animal");

        return app;
    }
}

public sealed record AdministerVaccinationRequest(DateOnly AdministeredDate, string? Notes);
