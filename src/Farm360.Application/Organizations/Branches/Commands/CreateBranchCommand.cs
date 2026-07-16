using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Organizations.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Commands;

public sealed record CreateBranchCommand(
    Guid OrganizationId,
    string BranchCode,
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
    string? WorkingHours,
    string? HolidayCalendar,
    bool IsHeadOffice) : IRequest<Guid>;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.BranchCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.ContactPhone).MaximumLength(30);
    }
}

public sealed class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Guid>
{
    private readonly IBranchRepository _repository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBranchCommandHandler(
        IBranchRepository repository,
        IOrganizationRepository organizationRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _organizationRepository = organizationRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        // Check if organization exists
        var orgExists = await _organizationRepository.GetByIdAsync(request.OrganizationId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("OrganizationId", "Organization not found.") });

        // Check if branch code is unique
        var exists = await _repository.ExistsByCodeAsync(tenantId, request.BranchCode, cancellationToken);
        if (exists)
            throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("BranchCode", "A branch with this code already exists for the current tenant.") });

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

        var branch = Branch.Create(
            tenantId,
            request.OrganizationId,
            request.BranchCode,
            request.Name,
            request.ContactEmail,
            request.IsHeadOffice);

        branch.UpdateDetails(
            request.Name,
            request.ContactEmail,
            request.ContactPhone,
            address,
            request.Latitude,
            request.Longitude,
            request.WorkingHours,
            request.HolidayCalendar);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _repository.Add(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);
            return branch.Id;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);
            throw;
        }
    }
}
