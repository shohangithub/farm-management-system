using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Api.Authorization;
using Farm360.Application.Documents;
using Farm360.Application.Documents.Queries;
using Farm360.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Farm360.Api.Endpoints;

/// <summary>
/// Customer documents a farm hands to its buyers and suppliers: the sale invoice of a sold
/// animal, the receipt for a payment in Payables &amp; Receivables, and a purchase order.
/// </summary>
/// <remarks>
/// Each document needs the permission of the data it prints (animals, finance, inventory), and
/// the query behind it checks the record's farm against the caller's allowed farms. Language
/// defaults to the farm's "Default report language" / "Bengali numerals" rules and can be
/// overridden with <c>?language=en|bn&amp;bengaliNumerals=true</c>.
/// </remarks>
public static class DocumentEndpoints
{
    private const string PdfContentType = "application/pdf";

    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/documents")
            .RequireAuthorization()
            .WithTags("Documents");

        group.MapGet("/sale-invoice/{animalId:guid}", GetSaleInvoice)
            .RequirePermission(PermissionConstants.Animals.View)
            .WithName("GetSaleInvoiceDocument")
            .WithSummary("Sale invoice / receipt of a sold animal, as a PDF.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType);

        group.MapGet("/payment-receipt/{id:guid}", GetPaymentReceipt)
            .RequirePermission(PermissionConstants.FinanceModule.View)
            .WithName("GetPaymentReceiptDocument")
            .WithSummary("Money receipt (collection) or payment voucher (supplier payment), as a PDF.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType);

        group.MapGet("/purchase-order/{id:guid}", GetPurchaseOrder)
            .RequirePermission(PermissionConstants.InventoryModule.View)
            .WithName("GetPurchaseOrderDocument")
            .WithSummary("Purchase order for the supplier, as a PDF.")
            .Produces(StatusCodes.Status200OK, contentType: PdfContentType);
    }

    private static async Task<IResult> GetSaleInvoice(
        Guid animalId,
        [FromQuery] string? language,
        [FromQuery] bool? bengaliNumerals,
        [FromServices] ISender sender,
        [FromServices] ICustomerDocumentRenderer renderer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var document = await sender.Send(new GetSaleInvoiceDocumentQuery(animalId, language, bengaliNumerals), cancellationToken);
        return Pdf(httpContext, renderer.RenderSaleInvoice(document), document.FileName);
    }

    private static async Task<IResult> GetPaymentReceipt(
        Guid id,
        [FromQuery] string? language,
        [FromQuery] bool? bengaliNumerals,
        [FromServices] ISender sender,
        [FromServices] ICustomerDocumentRenderer renderer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var document = await sender.Send(new GetPaymentReceiptDocumentQuery(id, language, bengaliNumerals), cancellationToken);
        return Pdf(httpContext, renderer.RenderPaymentReceipt(document), document.FileName);
    }

    private static async Task<IResult> GetPurchaseOrder(
        Guid id,
        [FromQuery] string? language,
        [FromQuery] bool? bengaliNumerals,
        [FromServices] ISender sender,
        [FromServices] ICustomerDocumentRenderer renderer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var document = await sender.Send(new GetPurchaseOrderDocumentQuery(id, language, bengaliNumerals), cancellationToken);
        return Pdf(httpContext, renderer.RenderPurchaseOrder(document), document.FileName);
    }

    /// <summary>
    /// Inline rather than attachment: the browser opens the PDF to print it there and then; the
    /// file name still applies when the reader saves it.
    /// </summary>
    private static IResult Pdf(HttpContext httpContext, byte[] content, string fileName)
    {
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(fileName);

        httpContext.Response.Headers.ContentDisposition = disposition.ToString();
        httpContext.Response.Headers.CacheControl = "no-store";
        return Results.File(content, PdfContentType);
    }
}
