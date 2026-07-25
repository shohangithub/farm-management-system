using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Organizations.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Organizations.Commands;

public record UpdateOrganizationCommand(
    Guid Id,
    string Name,
    string? LogoUrl,
    string ContactEmail,
    string? ContactPhone,
    string? BusinessRegistrationNumber,
    string? TradeLicenseNumber,
    string? TaxIdentificationNumber,
    string CurrencyCode,
    string TimeZoneId,
    string LanguageCode,
    string? Street,
    string? City,
    string? State,
    string? Country,
    string? ZipCode,
    BusinessType BusinessType) : IRequest, ITransactionalCommand;

public class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantService _tenantService;

    public UpdateOrganizationCommandValidator(IOrganizationRepository organizationRepository, ITenantService tenantService)
    {
        _organizationRepository = organizationRepository;
        _tenantService = tenantService;

        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.")
            .MustAsync(BeUniqueNameForUpdate).WithMessage("An organization with this name already exists for the current tenant.");

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("Contact email is required.")
            .EmailAddress().WithMessage("Contact email must be a valid email address.")
            .MaximumLength(150);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(30);

        RuleFor(x => x.CurrencyCode)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("Time zone is required.");

        RuleFor(x => x.LanguageCode)
            .NotEmpty().WithMessage("Language code is required.");

        RuleFor(x => x.BusinessType)
            .IsInEnum();
    }

    /// <summary>
    /// Checks uniqueness of the organization name within the tenant, excluding the current entity being updated.
    /// </summary>
    private async Task<bool> BeUniqueNameForUpdate(UpdateOrganizationCommand command, string name, CancellationToken cancellationToken)
    {
        var existing = await _organizationRepository.GetByNameAsync(_tenantService.TenantId, name, cancellationToken);
        // Unique if no org found, or the found org IS the one being updated
        return existing == null || existing.Id == command.Id;
    }
}

internal sealed class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand>
{
    private readonly IOrganizationRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrganizationCommandHandler(
        IOrganizationRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.Id);

        // Check uniqueness if name changed
        if (!organization.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _repository.ExistsByNameAsync(_tenantService.TenantId, request.Name, cancellationToken);
            if (exists)
                throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Name", "An organization with this name already exists for the current tenant.") });
        }

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(request.Street) || !string.IsNullOrWhiteSpace(request.City))
        {
            address = Address.Create(
                request.Street ?? "",
                request.City ?? "",
                request.State ?? "",
                request.Country ?? "",
                request.ZipCode ?? "");
        }

        organization.Update(
            request.Name,
            request.LogoUrl,
            request.ContactEmail,
            request.ContactPhone,
            request.BusinessRegistrationNumber,
            request.TradeLicenseNumber,
            request.TaxIdentificationNumber,
            request.CurrencyCode,
            request.TimeZoneId,
            request.LanguageCode,
            address,
            request.BusinessType);

        _repository.Update(organization);

        // SaveChangesAsync persists within the pipeline-managed transaction (TransactionBehavior).
        // Do NOT call BeginTransactionAsync here — the MediatR TransactionBehavior already wraps this command.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
