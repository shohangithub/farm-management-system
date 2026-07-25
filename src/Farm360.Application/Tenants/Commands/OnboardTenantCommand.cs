using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Interfaces.Repositories;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.ValueObjects;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using System.Text.RegularExpressions;

namespace Farm360.Application.Tenants.Commands;

public record OnboardTenantCommand(
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
    BusinessType BusinessType) : IRequest<Guid>, ITransactionalCommand;

public class OnboardTenantCommandValidator : AbstractValidator<OnboardTenantCommand>
{
    public OnboardTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.");

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
}

internal sealed class OnboardTenantCommandHandler : IRequestHandler<OnboardTenantCommand, Guid>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantUserRepository _tenantUserRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public OnboardTenantCommandHandler(
        ITenantRepository tenantRepository,
        ITenantUserRepository tenantUserRepository,
        IOrganizationRepository organizationRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantUserRepository = tenantUserRepository;
        _organizationRepository = organizationRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(OnboardTenantCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User must be authenticated to onboard a tenant.");
        }

        // 1. Generate slug from Name
        var slug = GenerateSlug(request.Name);

        // 2. Create Tenant
        var tenant = Tenant.Create(request.Name, slug, SubscriptionTier.Starter);
        await _tenantRepository.AddAsync(tenant, cancellationToken);

        // 3. Create TenantUser (Owner)
        var ownerRole = SystemRoleIds.Owner;
        var tenantUser = TenantUser.CreateOwner(tenant.Id, userId.Value, ownerRole);
        await _tenantUserRepository.AddAsync(tenantUser, cancellationToken);

        // 4. Create Organization
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
            tenant.Id,
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

        _organizationRepository.Add(organization);

        // SaveChangesAsync persists within the pipeline-managed transaction (TransactionBehavior).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return the Organization Id so the UI can redirect successfully.
        return organization.Id;
    }

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Guid.NewGuid().ToString("N")[..8];
            
        var slug = name.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
        
        if (string.IsNullOrWhiteSpace(slug))
            return Guid.NewGuid().ToString("N")[..8];
            
        return slug;
    }
}
