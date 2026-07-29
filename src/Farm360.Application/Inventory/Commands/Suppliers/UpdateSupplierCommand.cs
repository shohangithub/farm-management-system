using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Inventory.Commands.Suppliers;

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? Notes = null,
    bool IsActive = true) : IRequest;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand>
{
    private readonly ISupplierRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSupplierCommandHandler(ISupplierRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier with ID '{request.Id}' was not found.");

        supplier.UpdateDetails(request.Name, request.ContactPerson, request.Phone, request.Email, request.Address, request.Notes);
        supplier.SetActiveStatus(request.IsActive);

        _repository.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
