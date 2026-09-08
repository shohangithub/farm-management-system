using Farm360.Application.Finance.Commands;
using Farm360.Application.Finance.Queries;
using Farm360.Contracts.Finance;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Api.Endpoints.Finance;

public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder builder)
    {
        MapFinanceRoutes(builder, "/api/farms/{farmId:guid}/finance", "/api/finance");
        MapFinanceRoutes(builder, "/api/v1/farms/{farmId:guid}/finance", "/api/v1/finance");
        return builder;
    }

    private static void MapFinanceRoutes(IEndpointRouteBuilder builder, string farmRoutePrefix, string tenantRoutePrefix)
    {
        var group = builder.MapGroup(farmRoutePrefix)
            .WithTags("Finance")
            .RequireAuthorization();

        // Transaction routes (Paged, Filtered, CRUD, Export)
        group.MapGet("transactions", async (
            Guid farmId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] string? category = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? animalId = null,
            [FromQuery] Guid? batchId = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDesc = true,
            IMediator mediator = null!) =>
        {
            Domain.Finance.Enums.TransactionType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<Domain.Finance.Enums.TransactionType>(type, true, out var t))
                parsedType = t;

            Domain.Finance.Enums.TransactionCategory? parsedCategory = null;
            if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<Domain.Finance.Enums.TransactionCategory>(category, true, out var c))
                parsedCategory = c;

            var query = new GetPagedFinancialTransactionsQuery(
                farmId,
                pageNumber,
                pageSize,
                search,
                parsedType,
                parsedCategory,
                startDate,
                endDate,
                animalId,
                batchId,
                sortBy,
                sortDesc
            );

            var result = await mediator.Send(query);
            return Results.Ok(result);
        }).Produces<PagedFinancialTransactionsResult>();

        group.MapGet("transactions/{id:guid}", async (Guid farmId, Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFinancialTransactionByIdQuery(id, farmId));
            return Results.Ok(result);
        }).Produces<FinancialTransactionDto>();

        group.MapPut("transactions/{id:guid}", async (
            Guid farmId,
            Guid id,
            [FromBody] UpdateFinancialTransactionRequest request,
            IMediator mediator) =>
        {
            var command = new UpdateFinancialTransactionCommand(
                id,
                farmId,
                request.Category,
                request.AmountBdt,
                request.TransactionDate,
                request.Description,
                request.Notes,
                request.AnimalId,
                request.BatchId,
                request.ShedId
            );

            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<FinancialTransactionDto>();

        group.MapDelete("transactions/{id:guid}", async (Guid farmId, Guid id, IMediator mediator) =>
        {
            await mediator.Send(new DeleteFinancialTransactionCommand(id, farmId));
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        group.MapGet("transactions/export", async (
            Guid farmId,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] string? category = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            IMediator mediator = null!) =>
        {
            Domain.Finance.Enums.TransactionType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<Domain.Finance.Enums.TransactionType>(type, true, out var t))
                parsedType = t;

            Domain.Finance.Enums.TransactionCategory? parsedCategory = null;
            if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<Domain.Finance.Enums.TransactionCategory>(category, true, out var c))
                parsedCategory = c;

            // Fetch all matching records (up to 10000 for export)
            var query = new GetPagedFinancialTransactionsQuery(
                farmId,
                1,
                10000,
                search,
                parsedType,
                parsedCategory,
                startDate,
                endDate
            );

            var result = await mediator.Send(query);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TransactionId,Date,Type,Category,AmountBDT,Description,ReferenceId,Notes,CreatedAtUtc");
            foreach (var item in result.Items)
            {
                var desc = item.Description?.Replace(",", " ") ?? "";
                var refId = item.ReferenceId?.Replace(",", " ") ?? "";
                var notes = item.Notes?.Replace(",", " ") ?? "";
                sb.AppendLine($"{item.Id},{item.TransactionDate:yyyy-MM-dd},{item.Type},{item.Category},{item.AmountBdt},{desc},{refId},{notes},{item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/csv", $"finance_ledger_{DateTime.UtcNow:yyyyMMdd}.csv");
        });

        group.MapPost("transactions", async (
            Guid farmId,
            [FromBody] CreateFinancialTransactionRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var tenantId = tenantService.TenantId;
            var command = new CreateFinancialTransactionCommand(
                tenantId,
                farmId,
                request.Type,
                request.Category,
                request.AmountBdt,
                request.TransactionDate,
                request.ReferenceId,
                request.Notes
            );

            var result = await mediator.Send(command);
            return Results.Created($"/api/farms/{farmId}/finance/transactions/{result.Id}", result);
        }).Produces<FinancialTransactionDto>(StatusCodes.Status201Created);

        // --- NEW Sprint 1 Endpoints ---

        // Income/Expense Specific Commands
        group.MapPost("income", async (
            Guid farmId,
            [FromBody] RecordIncomeRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var tenantId = tenantService.TenantId;
            var command = new RecordIncomeCommand(tenantId, farmId, request.Category, request.AmountBdt, request.TransactionDate, request.Description, request.ReferenceId, request.Notes, request.AnimalId, request.BatchId, request.ShedId);
            var result = await mediator.Send(command);
            return Results.Created($"/api/farms/{farmId}/finance/transactions/{result.Id}", result);
        }).Produces<FinancialTransactionDto>(StatusCodes.Status201Created);

        group.MapPost("expense", async (
            Guid farmId,
            [FromBody] RecordExpenseRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var tenantId = tenantService.TenantId;
            var command = new RecordExpenseCommand(tenantId, farmId, request.Category, request.AmountBdt, request.TransactionDate, request.Description, request.ReferenceId, request.Notes, request.AnimalId, request.BatchId, request.ShedId);
            var result = await mediator.Send(command);
            return Results.Created($"/api/farms/{farmId}/finance/transactions/{result.Id}", result);
        }).Produces<FinancialTransactionDto>(StatusCodes.Status201Created);

        // Loans
        group.MapGet("loans", async (Guid farmId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetLoansQuery(farmId));
            return Results.Ok(result);
        }).Produces<List<LoanRecordDto>>();

        group.MapPost("loans", async (
            Guid farmId,
            [FromBody] CreateLoanRecordRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var tenantId = tenantService.TenantId;
            var command = new CreateLoanRecordCommand(tenantId, farmId, request.LenderName, request.PrincipalAmountBdt, request.InterestRatePercent, request.DisbursementDate, request.Schedule, request.Notes);
            var result = await mediator.Send(command);
            return Results.Created($"/api/farms/{farmId}/finance/loans/{result.Id}", result);
        }).Produces<LoanRecordDto>(StatusCodes.Status201Created);

        group.MapPost("loans/{loanId:guid}/repayments", async (
            Guid farmId,
            Guid loanId,
            [FromBody] RecordLoanRepaymentRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var tenantId = tenantService.TenantId;
            var command = new RecordLoanRepaymentCommand(tenantId, farmId, loanId, request.AmountBdt, request.RepaymentDate, request.ReferenceId, request.Notes);
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<LoanRecordDto>();

        // Animal Cost Ledger
        group.MapGet("animals/{animalId:guid}/ledger", async (Guid animalId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAnimalCostLedgerQuery(animalId));
            return Results.Ok(result);
        }).Produces<AnimalCostLedgerDto>();

        group.MapGet("animals/{animalId:guid}/breakeven", async (Guid animalId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBreakEvenCalculatorQuery(animalId));
            return Results.Ok(result);
        }).Produces<BreakEvenCalculatorDto>();

        // PnL Reports
        group.MapGet("reports/batch/{batchId:guid}/pnl", async (Guid farmId, Guid batchId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBatchPnLReportQuery(farmId, batchId));
            return Results.Ok(result);
        }).Produces<BatchPnLReportDto>();

        group.MapGet("reports/monthly", async (Guid farmId, int year, int month, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMonthlyPnLReportQuery(farmId, year, month));
            return Results.Ok(result);
        }).Produces<MonthlyPnLReportDto>();

        // Consolidated PnL is tenant-wide, so it should ideally be under api/finance instead of api/farms/{farmId}/finance
        // But for consistency we can leave it here or map it separately.
        var tenantGroup = builder.MapGroup(tenantRoutePrefix)
            .WithTags("Finance")
            .RequireAuthorization();

        tenantGroup.MapGet("reports/consolidated", async (int year, int month, Farm360.Application.Common.Interfaces.ITenantService tenantService, IMediator mediator) =>
        {
            var tenantId = tenantService.TenantId;
            var result = await mediator.Send(new GetConsolidatedPnLReportQuery(tenantId, year, month));
            return Results.Ok(result);
        }).Produces<ConsolidatedPnLReportDto>();

        // Dashboard
        group.MapGet("dashboard", async (Guid farmId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFinancialDashboardQuery(farmId));
            return Results.Ok(result);
        }).Produces<FinancialDashboardDto>();
    }
}
