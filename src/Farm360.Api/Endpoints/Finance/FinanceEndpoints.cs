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
    public static RouteGroupBuilder MapFinanceEndpoints(this RouteGroupBuilder group)
    {
        var financeGroup = group.MapGroup("/farms/{farmId:guid}/financial-transactions")
            .WithTags("Finance")
            .RequireAuthorization();

        financeGroup.MapGet("/", GetTransactions)
            .WithName("GetFinancialTransactions")
            .Produces<System.Collections.Generic.IReadOnlyList<FinancialTransactionDto>>(StatusCodes.Status200OK);

        financeGroup.MapGet("/summary", GetSummary)
            .WithName("GetFinancialTransactionSummary")
            .Produces<FinancialTransactionSummaryDto>(StatusCodes.Status200OK);

        financeGroup.MapPost("/", CreateTransaction)
            .WithName("CreateFinancialTransaction")
            .Produces<FinancialTransactionDto>(StatusCodes.Status201Created);

        return group;
    }

    private static async Task<IResult> GetTransactions(Guid farmId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetFinancialTransactionsQuery(farmId), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSummary(Guid farmId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetFinancialTransactionSummaryQuery(farmId), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateTransaction(
        Guid farmId,
        [FromBody] CreateFinancialTransactionRequest request,
        Farm360.Application.Common.Interfaces.ITenantService tenantService,
        ISender sender,
        CancellationToken ct)
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

        var result = await sender.Send(command, ct);
        return Results.Created($"/api/farms/{farmId}/financial-transactions/{result.Id}", result);
    }
}
