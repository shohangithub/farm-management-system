using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public sealed record DeleteFinancialTransactionCommand(Guid Id, Guid FarmId) : IRequest;

public sealed class DeleteFinancialTransactionCommandHandler : IRequestHandler<DeleteFinancialTransactionCommand>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFinancialTransactionCommandHandler(IFinancialTransactionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteFinancialTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.Id);

        await _repository.DeleteAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
