using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Organizations.ValueObjects;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Organizations.Commands;

public record CreateOrganizationCommand(
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
    BusinessType BusinessType) : IRequest<Guid>;

public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITenantService _tenantService;

    public CreateOrganizationCommandValidator(IOrganizationRepository organizationRepository, ITenantService tenantService)
    {
        _organizationRepository = organizationRepository;
        _tenantService = tenantService;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.")
            .MustAsync(BeUniqueName).WithMessage("An organization with this name already exists for the current tenant.");

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
            .NotEmpty();

        RuleFor(x => x.LanguageCode)
            .NotEmpty();
            
        RuleFor(x => x.BusinessType)
            .IsInEnum();
    }

    private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        var exists = await _organizationRepository.ExistsByNameAsync(_tenantService.TenantId, name, cancellationToken);
        return !exists;
    }
}

internal sealed class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, Guid>
{
    private readonly IOrganizationRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrganizationCommandHandler(
        IOrganizationRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
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

        var organization = Organization.Create(
            _tenantService.TenantId,
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

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        _repository.Add(organization);

        await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);

        return organization.Id;
    }
}
