using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Organizations.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Commands;

public sealed record UpdateBranchCommand(
    Guid Id,
    string Name,
    string ContactEmail,
    string? ContactPhone,
    string? Street,
    string? City,
    string? State,
    string? Country,
    string? ZipCode,
    double? Latitude,
    double? Longitude,
    BranchStatus Status,
    string? WorkingHours,
    string? HolidayCalendar,
    bool IsHeadOffice,
    string? ManagerUserId) : IRequest;

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.ContactPhone).MaximumLength(30);
    }
}

public sealed class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand>
{
    private readonly IBranchRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBranchCommandHandler(
        IBranchRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var branch = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Organizations.Branch), request.Id);

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(request.Street) || !string.IsNullOrWhiteSpace(request.City) || !string.IsNullOrWhiteSpace(request.State) || !string.IsNullOrWhiteSpace(request.Country) || !string.IsNullOrWhiteSpace(request.ZipCode))
        {
            address = Address.Create(
                request.Street ?? "",
                request.City ?? "",
                request.State ?? "",
                request.Country ?? "",
                request.ZipCode ?? "");
        }

        branch.UpdateDetails(
            request.Name,
            request.ContactEmail,
            request.ContactPhone,
            address,
            request.Latitude,
            request.Longitude,
            request.WorkingHours,
            request.HolidayCalendar);

        branch.ChangeStatus(request.Status);

        if (request.IsHeadOffice)
            branch.SetAsHeadOffice();
        else
            branch.UnsetHeadOffice();

        if (string.IsNullOrWhiteSpace(request.ManagerUserId))
            branch.RemoveManager();
        else if (Guid.TryParse(request.ManagerUserId, out var managerId))
            branch.AssignManager(managerId);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _repository.Update(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);
            throw;
        }
    }
}
