using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Documents.Queries;

/// <summary>
/// Gathers the sale invoice of a sold animal: the buyer, the animal line, and — for a credit
/// sale — what has been received and what is still due on its receivable bill.
/// </summary>
/// <param name="AnimalId">The sold animal.</param>
/// <param name="Language">"en" / "bn"; blank uses the farm's default report language.</param>
/// <param name="BengaliNumerals">Null uses the farm's "Bengali numerals" rule.</param>
public sealed record GetSaleInvoiceDocumentQuery(Guid AnimalId, string? Language = null, bool? BengaliNumerals = null)
    : IRequest<SaleInvoiceDocument>;

public sealed class GetSaleInvoiceDocumentQueryHandler : IRequestHandler<GetSaleInvoiceDocumentQuery, SaleInvoiceDocument>
{
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly ITradeBillRepository _bills;
    private readonly IFarmAccessService _farmAccess;
    private readonly IDocumentHeaderBuilder _headers;

    public GetSaleInvoiceDocumentQueryHandler(
        IAnimalRepository animals,
        IBreedRepository breeds,
        ITradeBillRepository bills,
        IFarmAccessService farmAccess,
        IDocumentHeaderBuilder headers)
    {
        _animals = animals;
        _breeds = breeds;
        _bills = bills;
        _farmAccess = farmAccess;
        _headers = headers;
    }

    public async Task<SaleInvoiceDocument> Handle(GetSaleInvoiceDocumentQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Tenant-filtered: another tenant's animal is simply not found.
        var animal = await _animals.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        if (!await _farmAccess.CanAccessFarmAsync(animal.FarmId, cancellationToken))
        {
            throw new ForbiddenAccessException("You do not have access to this farm.");
        }

        if (animal.SaleDate is not { } saleDate || animal.SalePriceBdt is not { } salePrice || salePrice <= 0)
        {
            throw new DomainException($"{animal.Tag.TagId} has not been sold, so there is no sale invoice to print.");
        }

        var header = await _headers.BuildAsync(animal.FarmId, request.Language, request.BengaliNumerals, cancellationToken);
        var breed = await _breeds.GetByIdAsync(animal.BreedId, cancellationToken);

        // A credit sale has a receivable bill keyed on the animal; a cash sale has none.
        var bill = await _bills.FindBySourceAsync(animal.FarmId, animal.Id, TransactionCategory.AnimalSale, cancellationToken);
        List<DocumentPaymentLine> payments = bill is null
            ? []
            : (await _bills.GetPaymentsAsync(bill.Id, cancellationToken))
                .OrderBy(p => p.PaymentDate)
                .ThenBy(p => p.CreatedAtUtc)
                .Select(p => new DocumentPaymentLine(p.PaymentDate, p.Method, p.Reference, p.AmountBdt))
                .ToList();

        var amount = bill?.AmountBdt ?? salePrice;
        var received = bill?.PaidBdt ?? amount;
        var adjusted = bill?.AdjustedBdt ?? 0m;
        var balance = bill is null ? 0m : Math.Max(0m, Math.Round(bill.OutstandingBdt, 2));

        var weight = animal.SaleWeightKg is > 0m ? animal.SaleWeightKg : null;
        var line = new SaleInvoiceLine(
            animal.Tag.TagId,
            header.Text(SpeciesText(animal.Species)),
            string.IsNullOrWhiteSpace(breed?.Name) ? null : breed.Name,
            header.Text(animal.Sex == AnimalSex.Female ? DocumentStrings.Female : DocumentStrings.Male),
            FormatAge(animal.DateOfBirth, saleDate, header),
            weight,
            weight is { } kg ? Math.Round(amount / kg, 2) : null,
            amount);

        var invoiceNumber = InvoiceNumber(bill, saleDate, animal.Tag.TagId);
        var buyerName = FirstNonBlank(bill?.PartyName, animal.BuyerName) ?? header.Text(DocumentStrings.CashCustomer);

        return new SaleInvoiceDocument(
            header,
            invoiceNumber,
            saleDate,
            buyerName,
            string.IsNullOrWhiteSpace(bill?.PartyPhone) ? null : bill.PartyPhone,
            line,
            amount,
            received,
            adjusted,
            balance,
            bill?.DueDate,
            bill is not null,
            payments,
            AmountInWords.For(amount, header.Language),
            $"sale-invoice_{FileSafe(invoiceNumber)}.pdf");
    }

    /// <summary>
    /// The credit bill's own number when there is one (it is what the buyer's ledger shows);
    /// otherwise a stable number derived from the sale, e.g. "SAL-20261124-BD-0042".
    /// </summary>
    internal static string InvoiceNumber(TradeBill? bill, DateOnly saleDate, string tag)
    {
        if (!string.IsNullOrWhiteSpace(bill?.ReferenceNo))
        {
            return bill.ReferenceNo.Trim();
        }

        var safeTag = FileSafe(tag).ToUpperInvariant();
        return $"SAL-{saleDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{safeTag}";
    }

    /// <summary>"2 y 3 m" / "২ বছর ৩ মাস"; days only for animals under a month old.</summary>
    internal static string FormatAge(DateOnly born, DateOnly at, DocumentHeader header)
    {
        if (at < born)
        {
            return "—";
        }

        var months = ((at.Year - born.Year) * 12) + at.Month - born.Month;
        if (at.Day < born.Day)
        {
            months--;
        }

        months = Math.Max(0, months);
        var years = months / 12;
        var remainder = months % 12;

        string Part(int value, LocalizedText unit) =>
            header.Digits(value.ToString(CultureInfo.InvariantCulture)) + " " + header.Text(unit);

        if (months == 0)
        {
            return Part(at.DayNumber - born.DayNumber, DocumentStrings.Days);
        }

        if (years == 0)
        {
            return Part(remainder, DocumentStrings.Months);
        }

        return remainder == 0
            ? Part(years, DocumentStrings.Years)
            : Part(years, DocumentStrings.Years) + " " + Part(remainder, DocumentStrings.Months);
    }

    internal static LocalizedText SpeciesText(AnimalSpecies species) => species switch
    {
        AnimalSpecies.CattleBeef => DocumentStrings.CattleBeef,
        AnimalSpecies.CattleDairy => DocumentStrings.CattleDairy,
        AnimalSpecies.Goat => DocumentStrings.Goat,
        AnimalSpecies.Sheep => DocumentStrings.Sheep,
        _ => species.ToString(),
    };

    /// <summary>Letters, digits and dashes only — safe in a file name and on a printed number.</summary>
    internal static string FileSafe(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            text.Append(char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-');
        }

        var result = text.ToString().Trim('-');
        return result.Length == 0 ? "document" : result;
    }

    private static string? FirstNonBlank(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? null : second.Trim();
    }
}
