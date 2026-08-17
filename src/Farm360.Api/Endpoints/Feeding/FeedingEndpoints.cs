using Farm360.Application.Feeding.Commands.ConsumptionLogs;
using Farm360.Application.Feeding.Commands.FeedFormulas;
using Farm360.Application.Feeding.Commands.FeedIngredients;
using Farm360.Application.Feeding.Commands.FeedingSchedules;
using Farm360.Application.Feeding.Queries.Analytics;
using Farm360.Application.Feeding.Queries.ConsumptionLogs;
using Farm360.Application.Feeding.Queries.FeedFormulas;
using Farm360.Application.Feeding.Queries.FeedIngredients;
using Farm360.Application.Feeding.Queries.FeedingSchedules;
using Farm360.Application.Feeding.Commands.FeedingRuleSets;
using Farm360.Application.Feeding.Queries.FeedingRuleSets;
using Farm360.Application.Feeding.Commands.AnimalFeedingPlans;
using Farm360.Application.Feeding.Queries.AnimalFeedingPlans;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.Queries.DailyFeedingEntries;
using Farm360.Application.Feeding.Commands.FeedingReconciliations;
using Farm360.Application.Feeding.Queries.FeedingReconciliations;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Feeding;

public static class FeedingEndpoints
{
    public static IEndpointRouteBuilder MapFeedingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/feeding")
            .WithTags("Smart Feeding & Nutrition")
            .RequireAuthorization();

        // ── Feed Ingredients ──────────────────────────────────────────────────
        group.MapGet("/ingredients", async (
            [FromQuery] bool includePreloaded,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedIngredientsQuery(includePreloaded), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/ingredients", async (
            [FromBody] CreateFeedIngredientCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/feeding/ingredients/{id}", new { id });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        group.MapPut("/ingredients/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateFeedIngredientCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route ID does not match request body ID.");
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Feed Formulas ─────────────────────────────────────────────────────
        group.MapGet("/formulas", async (
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize,
            [FromQuery] string? searchTerm,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedFormulasQuery(pageNumber <= 0 ? 1 : pageNumber, pageSize <= 0 ? 10 : pageSize, searchTerm), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapGet("/formulas/{id:guid}", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedFormulaDetailQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/formulas", async (
            [FromBody] CreateFeedFormulaCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/feeding/formulas/{id}", new { id });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        group.MapPut("/formulas/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateFeedFormulaCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route ID does not match request body ID.");
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Feeding Schedules ─────────────────────────────────────────────────
        group.MapGet("/schedules", async (
            [FromQuery] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedingSchedulesQuery(farmId), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/schedules", async (
            [FromBody] CreateFeedingScheduleCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/feeding/schedules/{id}", new { id });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        group.MapPut("/schedules/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateFeedingScheduleCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route ID does not match request body ID.");
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Daily Feed Consumption Logs ───────────────────────────────────────
        group.MapGet("/consumption", async (
            [FromQuery] Guid farmId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedConsumptionLogsQuery(farmId, fromDate, toDate), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/consumption", async (
            [FromBody] LogFeedConsumptionCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/feeding/consumption/{id}", new { id });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        // ── Analytics & FCR ───────────────────────────────────────────────────
        group.MapGet("/analytics/fcr", async (
            [FromQuery] Guid farmId,
            [FromQuery] Guid? shedId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFcrAnalyticsQuery(farmId, shedId), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        // ── Feeding Rule Sets ─────────────────────────────────────────────────
        group.MapGet("/rule-sets", async (
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedingRuleSetsQuery(), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/rule-sets", async (
            [FromBody] CreateFeedingRuleSetCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/feeding/rule-sets/{id}", new { id });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        group.MapPut("/rule-sets/{id:guid}", async (
            [FromRoute] Guid id,
            [FromBody] UpdateFeedingRuleSetCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route ID does not match request body ID.");
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Animal Feeding Plans ──────────────────────────────────────────────
        group.MapGet("/plans", async (
            [FromQuery] Guid farmId,
            [FromQuery] string? status,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedingPlansQuery(farmId, status), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/plans/assign", async (
            [FromBody] AssignAnimalFeedingPlanCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/feeding/plans/{id}", new { id });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        group.MapPut("/plans/{id:guid}/cancel", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelFeedingPlanCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");
        
        group.MapPut("/plans/{id:guid}/exclude", async (
            [FromRoute] Guid id,
            [FromBody] ExcludeAnimalFromPlanCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.PlanId) return Results.BadRequest();
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Daily Feeding Entries ─────────────────────────────────────────────
        group.MapGet("/entries/today", async (
            [FromQuery] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTodayFeedingEntriesQuery(farmId), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/entries/{id:guid}/confirm", async (
            [FromRoute] Guid id,
            [FromBody] ConfirmDailyFeedingEntryCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.EntryId) return Results.BadRequest();
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        group.MapPost("/entries/{id:guid}/adjust", async (
            [FromRoute] Guid id,
            [FromBody] AdjustDailyFeedingEntryCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.EntryId) return Results.BadRequest();
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        group.MapPost("/entries/{id:guid}/skip", async (
            [FromRoute] Guid id,
            [FromBody] SkipDailyFeedingEntryCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.EntryId) return Results.BadRequest();
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Reconciliations ───────────────────────────────────────────────────
        group.MapGet("/reconciliations", async (
            [FromQuery] Guid farmId,
            [FromQuery] string? status,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetReconciliationsQuery(farmId, status), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/reconciliations/{id:guid}/approve", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ApproveFeedingReconciliationCommand(id), ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        group.MapPost("/reconciliations/{id:guid}/reject", async (
            [FromRoute] Guid id,
            [FromBody] RejectFeedingReconciliationCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest();
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        return app;
    }
}
