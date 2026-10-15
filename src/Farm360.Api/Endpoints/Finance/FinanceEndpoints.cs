using Farm360.Application.Finance.Commands;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Services;
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
            [FromQuery] bool? isAutomated = null,
            [FromQuery] string? sourceModule = null,
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
                sortDesc,
                isAutomated,
                sourceModule
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
            [FromQuery] bool? isAutomated = null,
            [FromQuery] string? sourceModule = null,
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
                endDate,
                IsAutomated: isAutomated,
                SourceModule: sourceModule
            );

            var result = await mediator.Send(query);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("TransactionId,Date,Type,Category,AmountBDT,Description,ReferenceId,Notes,IsAutomated,SourceModule,CreatedAtUtc");
            foreach (var item in result.Items)
            {
                var desc = item.Description?.Replace(",", " ") ?? "";
                var refId = item.ReferenceId?.Replace(",", " ") ?? "";
                var notes = item.Notes?.Replace(",", " ") ?? "";
                var isAuto = item.IsAutomated ? "Yes" : "No";
                var srcMod = item.SourceModule ?? "Manual";
                sb.AppendLine($"{item.Id},{item.TransactionDate:yyyy-MM-dd},{item.Type},{item.Category},{item.AmountBdt},{desc},{refId},{notes},{isAuto},{srcMod},{item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}");
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
            var command = new RecordIncomeCommand(tenantId, farmId, request.Category, request.AmountBdt, request.TransactionDate, request.Description, request.ReferenceId, request.Notes, request.AnimalId, request.BatchId, request.ShedId, request.Credit);
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
            var command = new RecordExpenseCommand(tenantId, farmId, request.Category, request.AmountBdt, request.TransactionDate, request.Description, request.ReferenceId, request.Notes, request.AnimalId, request.BatchId, request.ShedId, request.Credit);
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

        // ── Investors & Profit-Sharing ──────────────────────────────────────
        group.MapGet("investors", async (
            Guid farmId,
            [FromQuery] bool includeInactive = false,
            IMediator mediator = null!) =>
        {
            var result = await mediator.Send(new GetInvestorsQuery(farmId, includeInactive));
            return Results.Ok(result);
        }).Produces<IReadOnlyList<InvestorDto>>();

        group.MapGet("investors/{id:guid}", async (
            Guid farmId,
            Guid id,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetInvestorByIdQuery(id));
            return Results.Ok(result);
        }).Produces<InvestorDto>();

        group.MapPost("investors", async (
            Guid farmId,
            [FromBody] CreateInvestorRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new CreateInvestorCommand(
                tenantService.TenantId,
                farmId,
                request.Name,
                request.InitialInvestmentBdt,
                request.InvestmentDate,
                request.AgreedProfitSharePercentage,
                request.Email,
                request.Phone,
                request.NationalId,
                request.Notes,
                request.ReferenceId
            );
            var result = await mediator.Send(command);
            return Results.Created($"/api/farms/{farmId}/finance/investors/{result.Id}", result);
        }).Produces<InvestorDto>(StatusCodes.Status201Created);

        group.MapPut("investors/{id:guid}", async (
            Guid farmId,
            Guid id,
            [FromBody] UpdateInvestorRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new UpdateInvestorCommand(
                tenantService.TenantId,
                id,
                request.Name,
                request.AgreedProfitSharePercentage,
                request.Email,
                request.Phone,
                request.NationalId,
                request.Notes
            );
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<InvestorDto>();

        group.MapPatch("investors/{id:guid}/status", async (
            Guid farmId,
            Guid id,
            [FromQuery] bool isActive,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new ToggleInvestorStatusCommand(tenantService.TenantId, id, isActive);
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<InvestorDto>();

        group.MapPost("investors/{id:guid}/transactions", async (
            Guid farmId,
            Guid id,
            [FromBody] RecordInvestorTransactionRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new RecordInvestorTransactionCommand(
                tenantService.TenantId,
                farmId,
                id,
                request.Type,
                request.AmountBdt,
                request.TransactionDate,
                request.ReferenceId,
                request.Notes
            );
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<InvestorTransactionDto>();

        group.MapGet("investors/pnl", async (
            Guid farmId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetInvestorPnLQuery(farmId, fromDate, toDate));
            return Results.Ok(result);
        }).Produces<InvestorPnLSummaryDto>();

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

        group.MapGet("reports/trial-balance", async (
            Guid farmId,
            [FromQuery] DateTime? asOfDate,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTrialBalanceQuery(farmId, asOfDate));
            return Results.Ok(result);
        }).Produces<TrialBalanceDto>();

        // compareDate defaults to the previous financial year end; pass noComparative=true to omit it.
        group.MapGet("reports/balance-sheet", async (
            Guid farmId,
            [FromQuery] DateTime? asOfDate,
            [FromQuery] DateTime? compareDate,
            [FromQuery] bool noComparative,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBalanceSheetQuery(farmId, asOfDate, noComparative ? DateTime.MinValue : compareDate));
            return Results.Ok(result);
        }).Produces<BalanceSheetDto>();

        // Statement of profit or loss; fromDate defaults to the financial year start.
        group.MapGet("reports/income-statement", async (
            Guid farmId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool? comparative,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetIncomeStatementQuery(farmId, fromDate, toDate, comparative ?? true));
            return Results.Ok(result);
        }).Produces<IncomeStatementDto>();

        group.MapGet("reports/fixed-asset-schedule", async (
            Guid farmId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFixedAssetScheduleQuery(farmId, fromDate, toDate));
            return Results.Ok(result);
        }).Produces<FixedAssetScheduleDto>();

        // ── Accounts setup: financial year, books start, opening balances, adjustments ──
        group.MapGet("accounting/settings", async (Guid farmId, IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetAccountingSettingsQuery(farmId))))
            .Produces<AccountingSettingsDto>();

        group.MapPut("accounting/settings", async (
            Guid farmId,
            [FromBody] SaveAccountingSettingsRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
            Results.Ok(await mediator.Send(new SaveAccountingSettingsCommand(tenantService.TenantId, farmId, request))))
            .Produces<AccountingSettingsDto>();

        group.MapGet("accounting/options", async (IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetAccountingSetupOptionsQuery())))
            .Produces<AccountingSetupOptionsDto>();

        group.MapGet("accounting/entries", async (Guid farmId, IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetBalanceSheetEntriesQuery(farmId))))
            .Produces<List<BalanceSheetEntryDto>>();

        group.MapPost("accounting/entries", async (
            Guid farmId,
            [FromBody] SaveBalanceSheetEntryRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateBalanceSheetEntryCommand(tenantService.TenantId, farmId, request));
            return Results.Created($"/api/farms/{farmId}/finance/accounting/entries/{result.Id}", result);
        }).Produces<BalanceSheetEntryDto>(StatusCodes.Status201Created);

        group.MapPut("accounting/entries/{id:guid}", async (Guid farmId, Guid id, [FromBody] SaveBalanceSheetEntryRequest request, IMediator mediator) =>
            Results.Ok(await mediator.Send(new UpdateBalanceSheetEntryCommand(farmId, id, request))))
            .Produces<BalanceSheetEntryDto>();

        group.MapDelete("accounting/entries/{id:guid}", async (Guid farmId, Guid id, IMediator mediator) =>
        {
            await mediator.Send(new DeleteBalanceSheetEntryCommand(farmId, id));
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        // Turns every posting up to the books start into opening balances.
        group.MapPost("accounting/entries/generate-opening", async (
            Guid farmId,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
            Results.Ok(await mediator.Send(new GenerateOpeningEntriesCommand(tenantService.TenantId, farmId))))
            .Produces<GenerateOpeningEntriesResultDto>();

        // ── Fixed-asset register ──
        group.MapGet("fixed-assets", async (Guid farmId, [FromQuery] DateTime? asOfDate, IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetFixedAssetsQuery(farmId, asOfDate))))
            .Produces<List<FixedAssetDto>>();

        group.MapPost("fixed-assets", async (
            Guid farmId,
            [FromBody] SaveFixedAssetRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateFixedAssetCommand(tenantService.TenantId, farmId, request));
            return Results.Created($"/api/farms/{farmId}/finance/fixed-assets/{result.Id}", result);
        }).Produces<FixedAssetDto>(StatusCodes.Status201Created);

        group.MapPut("fixed-assets/{id:guid}", async (
            Guid farmId,
            Guid id,
            [FromBody] SaveFixedAssetRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
            Results.Ok(await mediator.Send(new UpdateFixedAssetCommand(tenantService.TenantId, farmId, id, request))))
            .Produces<FixedAssetDto>();

        group.MapDelete("fixed-assets/{id:guid}", async (Guid farmId, Guid id, IMediator mediator) =>
        {
            await mediator.Send(new DeleteFixedAssetCommand(farmId, id));
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("fixed-assets/{id:guid}/dispose", async (
            Guid farmId,
            Guid id,
            [FromBody] DisposeFixedAssetRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
            Results.Ok(await mediator.Send(new DisposeFixedAssetCommand(tenantService.TenantId, farmId, id, request))))
            .Produces<FixedAssetDto>();

        group.MapDelete("fixed-assets/{id:guid}/dispose", async (
            Guid farmId,
            Guid id,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
            Results.Ok(await mediator.Send(new CancelFixedAssetDisposalCommand(tenantService.TenantId, farmId, id))))
            .Produces<FixedAssetDto>();

        // Daily / date-range income & expense cash book. Omit toDate (or pass the same date) for a
        // single-day report; omit both for today.
        group.MapGet("reports/income-expense", async (
            Guid farmId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IMediator mediator) =>
        {
            var from = fromDate ?? DateTime.Today;
            var result = await mediator.Send(new GetIncomeExpenseReportQuery(farmId, from, toDate ?? from));
            return Results.Ok(result);
        }).Produces<IncomeExpenseReportDto>();

        // Books vs registers (stock, herd, loans, investors) and a list of accounting issues.
        group.MapGet("accounting/health", async (Guid farmId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAccountingHealthQuery(farmId));
            return Results.Ok(result);
        }).Produces<AccountingHealthDto>();

        // Idempotent repair of records made before the accounting rules were enforced.
        group.MapPost("accounting/repair", async (
            Guid farmId,
            [FromBody] RepairLedgerRequest? request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new RepairLedgerCommand(tenantService.TenantId, farmId, request?.AdjustInventoryToStockCount ?? false);
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<LedgerRepairResultDto>();

        // ── Payables & receivables (credit book) ────────────────────────────
        group.MapGet("credit", async (Guid farmId, [FromQuery] bool includeSettled, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTradeCreditOverviewQuery(farmId, includeSettled));
            return Results.Ok(result);
        }).Produces<TradeCreditOverviewDto>();

        group.MapGet("credit/bills/{billId:guid}", async (Guid farmId, Guid billId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTradeBillQuery(farmId, billId));
            return Results.Ok(result);
        }).Produces<TradeBillDto>();

        // Put an existing cash-booked purchase or sale on credit.
        group.MapPost("credit/bills", async (Guid farmId, [FromBody] OpenTradeBillRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new OpenTradeBillCommand(farmId, request));
            return Results.Created($"/api/farms/{farmId}/finance/credit/bills/{result.Id}", result);
        }).Produces<TradeBillDto>(StatusCodes.Status201Created);

        group.MapPut("credit/bills/{billId:guid}", async (Guid farmId, Guid billId, [FromBody] UpdateBillTermsRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateTradeBillTermsCommand(farmId, billId, request));
            return Results.Ok(result);
        }).Produces<TradeBillDto>();

        group.MapDelete("credit/bills/{billId:guid}", async (Guid farmId, Guid billId, IMediator mediator) =>
        {
            await mediator.Send(new CancelTradeBillCommand(farmId, billId));
            return Results.NoContent();
        }).Produces(StatusCodes.Status204NoContent);

        group.MapPost("credit/bills/{billId:guid}/payments", async (Guid farmId, Guid billId, [FromBody] RecordBillPaymentRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordBillPaymentCommand(farmId, billId, request));
            return Results.Ok(result);
        }).Produces<TradeBillDto>();

        group.MapDelete("credit/bills/{billId:guid}/payments/{paymentId:guid}", async (Guid farmId, Guid billId, Guid paymentId, IMediator mediator) =>
        {
            var result = await mediator.Send(new VoidBillPaymentCommand(farmId, billId, paymentId));
            return Results.Ok(result);
        }).Produces<TradeBillDto>();

        group.MapPost("credit/bills/{billId:guid}/write-off", async (Guid farmId, Guid billId, [FromBody] WriteOffBillRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new WriteOffTradeBillCommand(farmId, billId, request));
            return Results.Ok(result);
        }).Produces<TradeBillDto>();

        // Full accounts statement: cash, P&L, costs, investors, profit share, shares, loans, ledger.
        group.MapGet("reports/full-accounts", async (
            Guid farmId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IMediator mediator) =>
        {
            var today = DateTime.Today;
            var from = fromDate ?? new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var result = await mediator.Send(new GetFullAccountsReportQuery(farmId, from, toDate ?? today));
            return Results.Ok(result);
        }).Produces<FullAccountsReportDto>();

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

        // ── Farm Share Market Endpoints ──────────────────────────────────────────
        group.MapGet("shares/overview", async (Guid farmId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetShareMarketOverviewQuery(farmId));
            return Results.Ok(result);
        }).Produces<ShareMarketOverviewDto>();

        group.MapPost("shares/configure", async (
            Guid farmId,
            [FromBody] ConfigureFarmSharesRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new ConfigureFarmSharesCommand(
                tenantService.TenantId,
                farmId,
                request.TotalShares,
                request.SharePriceBdt,
                request.OwnerShareCount,
                request.MinimumPurchaseShares,
                request.IsShareSaleOpen,
                request.ValuationNotes
            );
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<FarmShareConfigDto>();

        group.MapPost("shares/purchase", async (
            Guid farmId,
            [FromBody] PurchaseSharesRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new PurchaseSharesCommand(
                tenantService.TenantId,
                farmId,
                request.InvestorId,
                request.ShareCount,
                request.PricePerShareBdt,
                request.TransactionDate,
                request.ReferenceId,
                request.Notes
            );
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<ShareHoldingDto>();

        group.MapPost("shares/sell", async (
            Guid farmId,
            [FromBody] SellSharesRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new SellSharesCommand(
                tenantService.TenantId,
                farmId,
                request.InvestorId,
                request.ShareCount,
                request.PricePerShareBdt,
                request.TransactionDate,
                request.ReferenceId,
                request.Notes
            );
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<ShareHoldingDto>();

        group.MapPost("shares/transfer", async (
            Guid farmId,
            [FromBody] TransferSharesRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new TransferSharesCommand(
                tenantService.TenantId,
                farmId,
                request.FromInvestorId,
                request.ToInvestorId,
                request.ShareCount,
                request.PricePerShareBdt,
                request.TransactionDate,
                request.ReferenceId,
                request.Notes
            );
            var result = await mediator.Send(command);
            return Results.Ok(new { success = result });
        });

        group.MapPut("shares/valuation", async (
            Guid farmId,
            [FromBody] UpdateShareValuationRequest request,
            Farm360.Application.Common.Interfaces.ITenantService tenantService,
            IMediator mediator) =>
        {
            var command = new UpdateShareValuationCommand(
                tenantService.TenantId,
                farmId,
                request.NewSharePriceBdt,
                request.ValuationNotes
            );
            var result = await mediator.Send(command);
            return Results.Ok(result);
        }).Produces<FarmShareConfigDto>();

        group.MapGet("shares/holdings/{investorId:guid}", async (Guid investorId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetShareHoldingsByInvestorQuery(investorId));
            return Results.Ok(result);
        }).Produces<IReadOnlyList<ShareHoldingDto>>();

        group.MapGet("shares/transactions", async (
            Guid farmId,
            [FromQuery] Guid? investorId,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetShareTransactionsQuery(farmId, investorId));
            return Results.Ok(result);
        }).Produces<IReadOnlyList<ShareTransactionDto>>();

        // Dashboard
        group.MapGet("dashboard", async (Guid farmId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFinancialDashboardQuery(farmId));
            return Results.Ok(result);
        }).Produces<FinancialDashboardDto>();

        // ── Labour & overhead allocation (docs/32 GAP-2) ──────────────────────
        // Run monthly once the period's expenses are posted. Idempotent: re-running a period
        // skips transactions already allocated and recomputes ledger buckets rather than
        // incrementing them, so a repeat run changes nothing.
        group.MapPost("overhead/allocate", async (
            Guid farmId,
            [FromBody] AllocateOverheadRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new AllocateOverheadCommand(farmId, request.From, request.To, request.IsBackfill), ct);
            return Results.Ok(result);
        })
        // No .WithName(): MapFinanceRoutes is registered twice, under the legacy /api and the
        // current /api/v1 prefixes, so a fixed name here would be declared on two endpoints.
        // Routing rejects that when it builds the matcher on the FIRST REQUEST, not at startup —
        // the app boots cleanly and then throws on the first call. No other endpoint in this
        // file names itself. Covered by EndpointRoutingTests.EndpointNamesAreGloballyUnique.
        .WithSummary("Allocates farm labour and overhead onto animals for a period.")
        .Produces<OverheadAllocationResult>();
    }
}

/// <summary>Body for the overhead allocation run.</summary>
public sealed record AllocateOverheadRequest(DateOnly From, DateOnly To, bool IsBackfill = false);
