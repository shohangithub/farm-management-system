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

        // ── Nutrition & growth: what one animal ate against what it needs ─────
        group.MapGet("/nutrition/{animalId:guid}", async (
            [FromRoute] Guid animalId,
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new Farm360.Application.Feeding.Queries.Nutrition.GetAnimalNutritionQuery(animalId, from, to), ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        // ── Purchase planning: what the rations need for the next N days ──────
        group.MapPost("/requirements", async (
            [FromBody] Farm360.Application.Feeding.Queries.Planning.GetFeedRequirementQuery query,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        // Daily routine from the feed charts and recipes: per animal, mixing sheet, feeding times.
        group.MapPost("/routine", async (
            [FromBody] Farm360.Application.Feeding.Queries.Planning.GetDailyFeedingRoutineQuery query,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        // ── Who eats what: the board and the "Set ration" preview ─────────────
        group.MapGet("/board", async (
            [FromQuery] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new Farm360.Application.Feeding.Queries.Board.GetFeedingBoardQuery(farmId), ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/plans/preview", async (
            [FromBody] Farm360.Application.Feeding.Queries.Board.PreviewRationQuery query,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        // Old shed/batch schedules become group feeding plans (they never fed anything themselves).
        group.MapPost("/schedules/convert", async (
            [FromQuery] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new ConvertFeedingSchedulesCommand(farmId), ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Create}");

        group.MapPost("/plans/assign", async (
            [FromBody] AssignAnimalFeedingPlanCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var ids = await sender.Send(command, ct);
            return Results.Ok(new { ids });
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

        group.MapPost("/plans/sync-weights", async (
            [FromBody] SyncFeedingPlanWeightsCommand? command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command ?? new SyncFeedingPlanWeightsCommand(), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Daily Feeding Entries ─────────────────────────────────────────────
        group.MapGet("/entries/today", async (
            [FromQuery] Guid? farmId,
            [FromQuery] DateOnly? targetDate,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetTodayFeedingEntriesQuery(farmId, targetDate), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapGet("/animals/{animalId:guid}/summary", async (
            [FromRoute] Guid animalId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAnimalFeedingSummaryQuery(animalId), ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}")
        .WithSummary("Get feeding history, consumption metrics, active plans, and costs for a single animal");

        group.MapGet("/entries/{id:guid}/cost-breakdown", async (
            [FromRoute] Guid id,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFeedingEntryCostBreakdownQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        // Generate a day's entries from the plans — today, or a missed day (body: { date, farmId }).
        group.MapPost("/entries/generate", async (
            [FromBody] GenerateEntriesRequest? body,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new Farm360.Application.Feeding.Jobs.CreateDailyFeedingEntriesCommand(body?.Date, body?.FarmId), ct);
            return Results.Ok(new { message = body?.Date is { } d ? $"Feeding entries for {d:dd MMM yyyy} generated." : "Daily feeding entries generated successfully." });
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // Feed given outside the plan (a missed or extra feed, or an animal with no ration) — confirmed straight away.
        group.MapPost("/entries/manual", async (
            [FromBody] RecordManualFeedingCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) => Results.Ok(await sender.Send(command, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        // ── Daily feed sheet: record what was actually fed, with no plan ─────────
        group.MapGet("/sheet", async (
            [FromQuery] Guid farmId,
            [FromQuery] DateOnly date,
            [FromServices] ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new Farm360.Application.Feeding.Queries.DailyFeedSheets.GetDailyFeedSheetQuery(farmId, date), ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/sheet/copy-last", async (
            [FromBody] Farm360.Application.Feeding.Queries.DailyFeedSheets.GetLastFeedSheetQuery query,
            [FromServices] ISender sender,
            CancellationToken ct) => Results.Ok(await sender.Send(query, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.View}");

        group.MapPost("/sheet", async (
            [FromBody] Farm360.Application.Feeding.Commands.DailyFeedSheets.SaveDailyFeedSheetCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) => Results.Ok(await sender.Send(command, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        group.MapPost("/sheet/remove-entry", async (
            [FromBody] Farm360.Application.Feeding.Commands.DailyFeedSheets.RemoveFeedSheetEntryCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(command, ct);
            return Results.NoContent();
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

        group.MapPost("/sheet/end-plans", async (
            [FromBody] Farm360.Application.Feeding.Commands.DailyFeedSheets.EndFeedingPlansForAnimalsCommand command,
            [FromServices] ISender sender,
            CancellationToken ct) => Results.Ok(await sender.Send(command, ct)))
            .RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Edit}");

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

        // ── Per-animal feed allocation backfill (docs/32 GAP-1) ───────────────
        // One-off admin operation that fills in allocations for feeding entries recorded before
        // the per-animal grain existed. Idempotent: it only picks up entries that have none, so
        // it is safe to re-run or to work through history in nightly chunks.
        // Scoped to the caller's tenant (from the token, never the body) and to one farm; the
        // cross-tenant variant runs only from the startup/background job.
        group.MapPost("/allocations/backfill/{farmId:guid}", async (
            [FromRoute] Guid farmId,
            [FromBody] BackfillAllocationsRequest request,
            [FromServices] Farm360.Application.Common.Interfaces.ITenantService tenantService,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            if (tenantService.TenantId == Guid.Empty)
            {
                return Results.Forbid();
            }

            var command = new Farm360.Application.Feeding.Jobs.BackfillAnimalFeedAllocationsCommand(
                request.From,
                request.To,
                Math.Clamp(request.BatchSize ?? 500, 1, 2_000),
                Math.Clamp(request.MaxBatches ?? 200, 1, 500),
                TenantId: tenantService.TenantId,
                FarmId: farmId);
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Delete}")
          .WithSummary("Backfills per-animal feed allocations for a historical date range.");

        // One-off repair: zero skipped entries' allocations and reset every animal ledger's feed
        // bucket on the farm to its confirmed allocation total. Idempotent.
        group.MapPost("/allocations/resync-costs/{farmId:guid}", async (
            [FromRoute] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new Farm360.Application.Feeding.Jobs.ResyncAnimalFeedCostsCommand(farmId), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Delete}")
          .WithSummary("Recomputes every animal ledger's feed cost on a farm from confirmed feed allocations.");

        // One-off repair: apply every sold or dead animal's exit to feeding (plans ended, pending
        // entries from the exit day skipped, confirmed ones after it reversed with stock returned,
        // group entries re-split without it, ledger feed buckets re-synced). Idempotent.
        group.MapPost("/allocations/reprocess-exited-animals/{farmId:guid}", async (
            [FromRoute] Guid farmId,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new Farm360.Application.Feeding.Jobs.ReprocessExitedAnimalsCommand(farmId), ct);
            return Results.Ok(result);
        }).RequireAuthorization($"Permission:{PermissionConstants.FeedingModule.Delete}")
          .WithSummary("Stops feeding and feed cost for sold or dead animals from their exit date (repairs late-recorded exits).");

        return app;
    }
}

/// <summary>Body of POST /feeding/allocations/backfill/{farmId}.</summary>
public sealed record BackfillAllocationsRequest(DateOnly From, DateOnly To, int? BatchSize = null, int? MaxBatches = null);

/// <summary>Body of POST /feeding/entries/generate.</summary>
public sealed record GenerateEntriesRequest(DateOnly? Date, Guid? FarmId);
