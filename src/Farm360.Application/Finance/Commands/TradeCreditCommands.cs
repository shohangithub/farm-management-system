using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Commands;

// ══════════════════════════════════════════════════════════════════════════════
// PUT AN EXISTING PURCHASE / SALE ON CREDIT
// ══════════════════════════════════════════════════════════════════════════════

public sealed record OpenTradeBillCommand(Guid FarmId, OpenTradeBillRequest Request) : IRequest<TradeBillDto>;

public sealed class OpenTradeBillCommandHandler(
    IFinancialTransactionRepository transactions,
    ITradeBillRepository bills,
    ITradeCreditService credit,
    IUnitOfWork unitOfWork) : IRequestHandler<OpenTradeBillCommand, TradeBillDto>
{
    public async Task<TradeBillDto> Handle(OpenTradeBillCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        var posting = await transactions.GetByIdAsync(r.TransactionId, cancellationToken);
        if (posting is null || posting.FarmId != command.FarmId)
            throw new NotFoundException(nameof(FinancialTransaction), r.TransactionId);

        var bill = await credit.OpenAsync(posting,
            new CreditTerms(r.PartyName, r.PartyPhone, r.DueDate, r.PaidNowBdt, r.PaymentMethod, r.Notes),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return bill.ToDto(await bills.GetPaymentsAsync(bill.Id, cancellationToken), Today);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}

// ══════════════════════════════════════════════════════════════════════════════
// PAYMENTS
// ══════════════════════════════════════════════════════════════════════════════

public sealed record RecordBillPaymentCommand(Guid FarmId, Guid BillId, RecordBillPaymentRequest Request) : IRequest<TradeBillDto>;

public sealed class RecordBillPaymentCommandHandler(
    ITradeBillRepository bills,
    ITradeCreditService credit,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordBillPaymentCommand, TradeBillDto>
{
    public async Task<TradeBillDto> Handle(RecordBillPaymentCommand command, CancellationToken cancellationToken)
    {
        var bill = await TradeBillLookup.LoadAsync(bills, command.FarmId, command.BillId, cancellationToken);
        var r = command.Request;
        if (r.PaymentDate > DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("A payment cannot be dated in the future.");

        await credit.RecordPaymentAsync(bill, r.AmountBdt, r.PaymentDate, r.Method, r.Reference, r.Notes, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return bill.ToDto(await bills.GetPaymentsAsync(bill.Id, cancellationToken), DateOnly.FromDateTime(DateTime.Today));
    }
}

public sealed record VoidBillPaymentCommand(Guid FarmId, Guid BillId, Guid PaymentId) : IRequest<TradeBillDto>;

public sealed class VoidBillPaymentCommandHandler(
    ITradeBillRepository bills,
    ITradeCreditService credit,
    IUnitOfWork unitOfWork) : IRequestHandler<VoidBillPaymentCommand, TradeBillDto>
{
    public async Task<TradeBillDto> Handle(VoidBillPaymentCommand command, CancellationToken cancellationToken)
    {
        var bill = await TradeBillLookup.LoadAsync(bills, command.FarmId, command.BillId, cancellationToken);
        var payment = await bills.GetPaymentByIdAsync(command.PaymentId, cancellationToken);
        if (payment is null || payment.BillId != bill.Id)
            throw new NotFoundException(nameof(TradeBillPayment), command.PaymentId);

        await credit.VoidPaymentAsync(bill, payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return bill.ToDto(await bills.GetPaymentsAsync(bill.Id, cancellationToken), DateOnly.FromDateTime(DateTime.Today));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// WRITE-OFF, CANCEL, TERMS
// ══════════════════════════════════════════════════════════════════════════════

public sealed record WriteOffTradeBillCommand(Guid FarmId, Guid BillId, WriteOffBillRequest Request) : IRequest<TradeBillDto>;

public sealed class WriteOffTradeBillCommandHandler(
    ITradeBillRepository bills,
    ITradeCreditService credit,
    IUnitOfWork unitOfWork) : IRequestHandler<WriteOffTradeBillCommand, TradeBillDto>
{
    public async Task<TradeBillDto> Handle(WriteOffTradeBillCommand command, CancellationToken cancellationToken)
    {
        var bill = await TradeBillLookup.LoadAsync(bills, command.FarmId, command.BillId, cancellationToken);
        var r = command.Request;

        await credit.WriteOffAsync(bill, r.AmountBdt, r.Date, r.Reason, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return bill.ToDto(await bills.GetPaymentsAsync(bill.Id, cancellationToken), DateOnly.FromDateTime(DateTime.Today));
    }
}

public sealed record CancelTradeBillCommand(Guid FarmId, Guid BillId) : IRequest;

public sealed class CancelTradeBillCommandHandler(
    ITradeBillRepository bills,
    ITradeCreditService credit,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelTradeBillCommand>
{
    public async Task Handle(CancelTradeBillCommand command, CancellationToken cancellationToken)
    {
        var bill = await TradeBillLookup.LoadAsync(bills, command.FarmId, command.BillId, cancellationToken);
        await credit.CancelAsync(bill, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UpdateTradeBillTermsCommand(Guid FarmId, Guid BillId, UpdateBillTermsRequest Request) : IRequest<TradeBillDto>;

public sealed class UpdateTradeBillTermsCommandHandler(
    ITradeBillRepository bills,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateTradeBillTermsCommand, TradeBillDto>
{
    public async Task<TradeBillDto> Handle(UpdateTradeBillTermsCommand command, CancellationToken cancellationToken)
    {
        var bill = await TradeBillLookup.LoadAsync(bills, command.FarmId, command.BillId, cancellationToken);
        var r = command.Request;

        bill.UpdateTerms(r.PartyName, r.PartyPhone, r.DueDate, r.Notes);
        bills.Update(bill);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return bill.ToDto(await bills.GetPaymentsAsync(bill.Id, cancellationToken), DateOnly.FromDateTime(DateTime.Today));
    }
}

internal static class TradeBillLookup
{
    public static async Task<TradeBill> LoadAsync(ITradeBillRepository bills, Guid farmId, Guid billId, CancellationToken cancellationToken)
    {
        var bill = await bills.GetByIdAsync(billId, cancellationToken);
        if (bill is null || bill.FarmId != farmId)
            throw new NotFoundException(nameof(TradeBill), billId);
        return bill;
    }
}
