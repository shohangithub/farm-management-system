using Farm360.Application.Inventory.Commands.InventoryItems;
using Farm360.Application.Inventory.Commands.PurchaseOrders;
using Farm360.Application.Inventory.Queries.PurchaseOrders;
using Farm360.Application.Inventory.Commands.StockTransactions;
using Farm360.Application.Inventory.Commands.Suppliers;
using Farm360.Application.Inventory.Queries.InventoryItems;
using Farm360.Application.Inventory.Queries.Reports;
using Farm360.Application.Inventory.Queries.StockTransactions;
using Farm360.Application.Inventory.Queries.Suppliers;
using Farm360.Domain.Inventory.Enums;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Endpoints.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inventory")
            .WithTags("Inventory Module")
            .RequireAuthorization();

        // ── Inventory Items Catalog ───────────────────────────────────────────
        group.MapGet("/items", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? farmId = null,
            [FromQuery] InventoryCategory? category = null,
            [FromQuery] InventoryStatus? status = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDesc = false,
            ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var query = new GetInventoryItemsQuery(pageNumber, pageSize, farmId, category, status, search, sortBy, sortDesc);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetInventoryItems");

        group.MapGet("/items/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetInventoryItemDetailQuery(id);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetInventoryItemDetail");

        group.MapPost("/items", async (
            [FromBody] CreateInventoryItemCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/items/{id}", new { id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Create}")
        .WithName("CreateInventoryItem");

        group.MapPut("/items/{id:guid}", async (
            Guid id,
            [FromBody] UpdateInventoryItemCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route ID does not match command ID.");
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Edit}")
        .WithName("UpdateInventoryItem");

        group.MapDelete("/items/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DeleteInventoryItemCommand(id);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Delete}")
        .WithName("DeleteInventoryItem");

        // ── Stock Transactions & Ledger ───────────────────────────────────────
        group.MapPost("/transactions/stock-in", async (
            [FromBody] RecordStockInCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/transactions/{id}", new { id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Create}")
        .WithName("RecordStockIn");

        group.MapPost("/transactions/stock-out", async (
            [FromBody] RecordStockOutCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/transactions/{id}", new { id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Create}")
        .WithName("RecordStockOut");

        group.MapGet("/transactions", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? farmId = null,
            [FromQuery] Guid? inventoryItemId = null,
            [FromQuery] StockTransactionType? transactionType = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDesc = false,
            ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var query = new GetStockTransactionsQuery(pageNumber, pageSize, farmId, inventoryItemId, transactionType, fromDate, toDate, search, sortBy, sortDesc);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetStockTransactions");

        // ── Supplier Management ───────────────────────────────────────────────
        group.MapGet("/suppliers", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDesc = false,
            ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var query = new GetSuppliersQuery(pageNumber, pageSize, search, sortBy, sortDesc);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetSuppliers");

        group.MapPost("/suppliers", async (
            [FromBody] CreateSupplierCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/suppliers/{id}", new { id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Create}")
        .WithName("CreateSupplier");

        group.MapPut("/suppliers/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSupplierCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            if (id != command.Id) return Results.BadRequest("Route ID does not match command ID.");
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Edit}")
        .WithName("UpdateSupplier");

        group.MapDelete("/suppliers/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DeleteSupplierCommand(id);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Delete}")
        .WithName("DeleteSupplier");

        // ── Valuation Report ─────────────────────────────────────────────────
        group.MapGet("/reports/valuation", async (
            [FromQuery] Guid farmId,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetInventoryValuationReportQuery(farmId);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetInventoryValuationReport");

        // ── Current Stock Summary ────────────────────────────────────────────
        group.MapGet("/reports/current-stock/summary", async (
            [FromQuery] Guid farmId,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetCurrentStockSummaryQuery(farmId);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetCurrentStockSummary");

        // ── Purchase Orders ───────────────────────────────────────────────────
        group.MapGet("/purchase-orders", async (
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? farmId = null,
            [FromQuery] Guid? supplierId = null,
            [FromQuery] PurchaseOrderStatus? status = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] bool sortDesc = false,
            ISender sender = null!,
            CancellationToken ct = default) =>
        {
            var query = new GetPurchaseOrdersQuery(pageNumber, pageSize, farmId, supplierId, status, search, sortBy, sortDesc);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetPurchaseOrders");

        group.MapGet("/purchase-orders/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetPurchaseOrderByIdQuery(id);
            var result = await sender.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.View}")
        .WithName("GetPurchaseOrderById");

        group.MapPost("/purchase-orders", async (
            [FromBody] CreatePurchaseOrderCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(command, ct);
            return Results.Created($"/api/v1/inventory/purchase-orders/{id}", new { id });
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Create}")
        .WithName("CreatePurchaseOrder");

        group.MapPost("/purchase-orders/{id:guid}/approve", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ApprovePurchaseOrderCommand(id);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Edit}")
        .WithName("ApprovePurchaseOrder");

        group.MapPost("/purchase-orders/{id:guid}/fulfill", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new FulfillPurchaseOrderCommand(id);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .RequireAuthorization($"Permission:{PermissionConstants.InventoryModule.Edit}")
        .WithName("FulfillPurchaseOrder");

        return app;
    }
}
