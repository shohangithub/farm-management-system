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
        var group = builder.MapGroup("api/farms/{farmId:guid}/finance")
            .WithTags("Finance")
            .RequireAuthorization("RequireTenant");

        // Existing transaction routes
        group.MapGet("transactions", async (Guid farmId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFinancialTransactionsQuery(farmId));
            return Results.Ok(result);
        }).Produces<List<FinancialTransactionDto>>();

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
        var tenantGroup = builder.MapGroup("api/finance")
            .WithTags("Finance")
            .RequireAuthorization("RequireTenant");

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

        return builder;
    }
}
