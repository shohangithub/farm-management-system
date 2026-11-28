using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

// ══════════════════════════════════════════════════════════════════════════════
// REGISTER ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Registers a new animal. Creates the Animal aggregate and raises AnimalRegisteredEvent.
/// Permission: animals:write
/// </summary>
public sealed record RegisterAnimalCommand(
    Guid FarmId,
    string TagId,
    TagType TagType,
    AnimalSpecies Species,
    Guid BreedId,
    AnimalSex Sex,
    DateOnly DateOfBirth,
    AcquisitionType AcquisitionType,
    DateOnly AcquisitionDate,
    decimal? AcquisitionPriceBdt,
    string? Notes,
    Guid? ShedId,
    Guid? PenId,
    // Opt out of the health programmes that start automatically for purchased animals.
    bool SkipHealthProgramme = false,
    // Paid from (purchased animals): a money account (null = the farm's default), OR on credit
    // (a supplier bill is raised at registration), OR directly by a partner (books their capital).
    Guid? MoneyAccountId = null,
    Guid? PaidByPartnerId = null,
    Farm360.Contracts.Finance.CreditTermsRequest? Credit = null) : IRequest<AnimalDto>, Farm360.Application.Common.Behaviors.ITransactionalCommand;

public sealed class RegisterAnimalCommandValidator : AbstractValidator<RegisterAnimalCommand>
{
    public RegisterAnimalCommandValidator(IAnimalRepository repository, IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.FarmId)
            .NotEmpty().WithMessage("Farm is required.");

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("Tag ID is required.")
            .MaximumLength(50).WithMessage("Tag ID cannot exceed 50 characters.");

        // Async uniqueness check — uses IAnimalRepository (tenant-scoped by Global Query Filter)
        RuleFor(x => x.TagId)
            .MustAsync(async (tagId, ct) => !await repository.TagExistsAsync(tagId, null, ct))
            .WithMessage("An animal with this Tag ID already exists in your farm.");

        RuleFor(x => x.BreedId)
            .NotEmpty().WithMessage("Breed is required.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.AcquisitionDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Acquisition date cannot be in the future.")
            .GreaterThanOrEqualTo(x => x.DateOfBirth)
            .WithMessage("Acquisition date cannot be before date of birth.");

        RuleFor(x => x.AcquisitionPriceBdt)
            .GreaterThan(0).WithMessage("Acquisition price must be greater than zero.")
            .When(x => x.AcquisitionPriceBdt.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.PaidByPartnerId)
            .Must(p => p is null)
            .When(x => x.Credit is not null)
            .WithMessage("Choose one: paid from an account, on credit, or paid directly by a partner.");

        RuleFor(x => x.Credit!.PartyName)
            .NotEmpty().WithMessage("Enter the seller's name for a purchase on credit.")
            .When(x => x.Credit is not null);

        RuleFor(x => x.Credit!.PaidNowBdt)
            .GreaterThanOrEqualTo(0)
            .LessThan(x => x.AcquisitionPriceBdt ?? 0m)
            .When(x => x.Credit is not null)
            .WithMessage("The amount paid now must be less than the purchase price (otherwise it is a cash purchase).");
    }
}

public sealed class RegisterAnimalCommandHandler(
    IAnimalRepository repository,
    ITenantRepository tenantRepository,
    IUnitOfWork unitOfWork,
    ITenantService tenantService,
    ICurrentUserService currentUser,
    IPublisher publisher,
    IFarmClock? clock = null,
    Farm360.Application.Finance.Services.ITradeCreditService? credit = null,
    Farm360.Application.Finance.Services.IMoneyAccountService? money = null) : IRequestHandler<RegisterAnimalCommand, AnimalDto>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<AnimalDto> Handle(RegisterAnimalCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantService.TenantId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("TenantId", "Tenant not found.") });

        // Lock the tenant row first so two registrations at once cannot both take the last slot.
        await tenantRepository.AcquireQuotaLockAsync(tenantService.TenantId, cancellationToken);
        var (_, _, animalsCount) = await tenantRepository.GetTenantUsageCountsAsync(tenantService.TenantId, cancellationToken);
        if (animalsCount >= tenant.MaxAnimals)
        {
            throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure(
                "TagId",
                $"Your {tenant.SubscriptionTier} plan allows up to {tenant.MaxAnimals} animal(s). Upgrade your subscription to register more.") });
        }

        var tag = AnimalTag.Create(request.TagId, request.TagType);

        var animal = Animal.Create(
            tenantId: tenantService.TenantId,
            farmId: request.FarmId,
            tag: tag,
            species: request.Species,
            breedId: request.BreedId,
            sex: request.Sex,
            dateOfBirth: request.DateOfBirth,
            acquisitionType: request.AcquisitionType,
            acquisitionDate: request.AcquisitionDate,
            acquisitionPriceBdt: request.AcquisitionPriceBdt,
            notes: request.Notes);

        if (request.ShedId.HasValue)
        {
            // The animal has been in this pen since it arrived, not since the day it was entered in
            // the app: dating the first placement "today" would reject back-dated sales, deaths and
            // other on-farm records between acquisition and today. The validator already keeps the
            // acquisition date out of the future; the Min is a guard for callers that skip it.
            var userId = currentUser.UserId ?? Guid.Empty;
            var placedOn = request.AcquisitionDate <= _clock.Today ? request.AcquisitionDate : _clock.Today;
            animal.TransferToShed(
                request.ShedId.Value,
                request.PenId,
                placedOn,
                userId,
                "Initial Placement");
        }

        repository.Add(animal);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var domainEvents = animal.DomainEvents.ToList();
        animal.ClearDomainEvents();
        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }

        // The purchase was booked by Finance (AnimalRegisteredEvent) as a cash purchase. Now say how
        // it was paid: on credit (a supplier bill), directly by a partner, or from a chosen account.
        if (request.AcquisitionType != AcquisitionType.BornOnFarm && (request.AcquisitionPriceBdt ?? 0m) > 0m)
        {
            const Farm360.Domain.Finance.Enums.TransactionCategory purchase = Farm360.Domain.Finance.Enums.TransactionCategory.AnimalPurchase;
            if (request.Credit is { } terms && credit is not null)
            {
                await credit.OpenForSourceAsync(animal.FarmId, animal.Id, purchase,
                    new Farm360.Application.Finance.Services.CreditTerms(terms.PartyName, terms.PartyPhone, terms.DueDate, terms.PaidNowBdt,
                        terms.PaymentMethod, terms.Notes, MoneyAccountId: terms.MoneyAccountId ?? request.MoneyAccountId),
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else if (money is not null && (request.MoneyAccountId is not null || request.PaidByPartnerId is not null))
            {
                await money.ApplyToSourceAsync(animal.FarmId, animal.Id, purchase,
                    new Farm360.Application.Finance.Services.PaymentSource(request.MoneyAccountId, request.PaidByPartnerId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        // A purchased animal starts the farm's automatic health programmes (e.g. the 108-day market
        // cattle programme) from its acquisition date, unless the user opted out for this animal.
        if (request.AcquisitionType == AcquisitionType.Purchased && !request.SkipHealthProgramme)
        {
            await publisher.Publish(new Farm360.Application.Health.Programmes.PurchasedAnimalRegisteredNotification(
                animal.TenantId, animal.FarmId, animal.Id, animal.Species, animal.AcquisitionDate), cancellationToken);
        }

        return animal.ToDto();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// UPDATE ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Updates existing animal details.
/// Permission: animals:update
/// </summary>
public sealed record UpdateAnimalCommand(
    Guid Id,
    string TagId,
    TagType TagType,
    AnimalSpecies Species,
    Guid BreedId,
    AnimalSex Sex,
    DateOnly DateOfBirth,
    AcquisitionType AcquisitionType,
    DateOnly AcquisitionDate,
    decimal? AcquisitionPriceBdt,
    string? Notes) : IRequest<AnimalDto>;

public sealed class UpdateAnimalCommandValidator : AbstractValidator<UpdateAnimalCommand>
{
    public UpdateAnimalCommandValidator(IAnimalRepository repository, IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Animal ID is required.");

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage("Tag ID is required.")
            .MaximumLength(50).WithMessage("Tag ID cannot exceed 50 characters.");

        // Unique tag check (excluding current animal)
        RuleFor(x => x)
            .MustAsync(async (command, ct) => !await repository.TagExistsAsync(command.TagId, command.Id, ct))
            .WithMessage("An animal with this Tag ID already exists in your farm.");

        RuleFor(x => x.BreedId)
            .NotEmpty().WithMessage("Breed is required.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.AcquisitionDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Acquisition date cannot be in the future.")
            .GreaterThanOrEqualTo(x => x.DateOfBirth)
            .WithMessage("Acquisition date cannot be before date of birth.");

        RuleFor(x => x.AcquisitionPriceBdt)
            .GreaterThan(0).WithMessage("Acquisition price must be greater than zero.")
            .When(x => x.AcquisitionPriceBdt.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}

public sealed class UpdateAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<UpdateAnimalCommand, AnimalDto>
{
    public async Task<AnimalDto> Handle(UpdateAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.Id);

        var tag = AnimalTag.Create(request.TagId, request.TagType);

        animal.UpdateDetails(
            tag: tag,
            species: request.Species,
            breedId: request.BreedId,
            sex: request.Sex,
            dateOfBirth: request.DateOfBirth,
            acquisitionType: request.AcquisitionType,
            acquisitionDate: request.AcquisitionDate,
            acquisitionPriceBdt: request.AcquisitionPriceBdt,
            notes: request.Notes);

        // Note: EF Core tracks changes automatically. No need for repository.Update(animal)
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var domainEvents = animal.DomainEvents.ToList();
        animal.ClearDomainEvents();
        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }

        return animal.ToDto();
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// RECORD WEIGHT
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Records a new weight measurement for an animal.
/// <para>
/// Either a scale weight (<see cref="WeightKg"/>), or a tape measure (<see cref="LengthInch"/> +
/// <see cref="GirthInch"/>, <see cref="Method"/> = Tape). With Tape the server computes the kg
/// itself via <see cref="TapeWeight"/>; any kg the client sends is ignored. When
/// <see cref="Method"/> is omitted it is Tape if a length or girth is given and no kg, else Scale.
/// </para>
/// Permission: animals:write
/// </summary>
public sealed record RecordWeightCommand(
    Guid AnimalId,
    decimal? WeightKg,
    DateOnly RecordedDate,
    string? Notes,
    WeightMethod? Method = null,
    decimal? LengthInch = null,
    decimal? GirthInch = null) : IRequest<WeightRecordDto>
{
    /// <summary>The method actually used: as given, else inferred from which inputs were sent.</summary>
    public WeightMethod EffectiveMethod =>
        Method ?? (WeightKg is null && (LengthInch is not null || GirthInch is not null)
            ? WeightMethod.Tape
            : WeightMethod.Scale);
}

public sealed class RecordWeightCommandValidator : AbstractValidator<RecordWeightCommand>
{
    public RecordWeightCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.AnimalId).NotEmpty();

        RuleFor(x => x.Method).IsInEnum().When(x => x.Method is not null);

        When(x => x.EffectiveMethod == WeightMethod.Scale, () =>
        {
            RuleFor(x => x.WeightKg)
                .NotNull().WithMessage("Enter the weight in kg.")
                .GreaterThan(0).WithMessage("Weight must be greater than zero.")
                .LessThanOrEqualTo(2000).WithMessage("Weight cannot exceed 2000 kg.");
        });

        When(x => x.EffectiveMethod == WeightMethod.Tape, () =>
        {
            RuleFor(x => x.LengthInch)
                .NotNull().WithMessage("Enter the body length for a tape-measure weight.")
                .Must(v => v is null || TapeWeight.IsPlausible(v.Value)).WithMessage(TapeWeight.LengthOutOfRangeMessage);

            RuleFor(x => x.GirthInch)
                .NotNull().WithMessage("Enter the heart girth for a tape-measure weight.")
                .Must(v => v is null || TapeWeight.IsPlausible(v.Value)).WithMessage(TapeWeight.GirthOutOfRangeMessage);

            RuleFor(x => x)
                .Must(x => TapeWeight.Compute(x.LengthInch!.Value, x.GirthInch!.Value) <= TapeWeight.MaxKg)
                .When(x => x.LengthInch is { } l && x.GirthInch is { } g && TapeWeight.IsPlausible(l) && TapeWeight.IsPlausible(g))
                .WithName("Weight")
                .WithMessage(TapeWeight.EstimateTooHighMessage);
        });

        RuleFor(x => x.RecordedDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Recorded date cannot be in the future.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);
    }
}

public sealed class RecordWeightCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IPublisher publisher) : IRequestHandler<RecordWeightCommand, WeightRecordDto>
{
    public async Task<WeightRecordDto> Handle(RecordWeightCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdWithWeightsAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var recordedBy = currentUser.UserId ?? Guid.Empty;
        WeightRecord record;
        if (request.EffectiveMethod == WeightMethod.Tape)
        {
            // The kg comes from the formula, never from the client.
            if (request.LengthInch is not { } length || request.GirthInch is not { } girth)
            {
                throw new Farm360.Application.Common.Exceptions.ValidationException(
                    [new FluentValidation.Results.ValidationFailure(nameof(request.LengthInch), "Enter the body length and heart girth for a tape-measure weight.")]);
            }

            record = animal.RecordTapeWeight(length, girth, request.RecordedDate, recordedBy, request.Notes);
        }
        else
        {
            if (request.WeightKg is not { } kg)
            {
                throw new Farm360.Application.Common.Exceptions.ValidationException(
                    [new FluentValidation.Results.ValidationFailure(nameof(request.WeightKg), "Enter the weight in kg.")]);
            }

            record = animal.RecordWeight(Weight.Create(kg), request.RecordedDate, recordedBy, request.Notes);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var domainEvents = animal.DomainEvents.ToList();
        animal.ClearDomainEvents();
        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }

        return record.ToDto();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// SELL ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Sells the animal. Transitions status to Sold. Raises AnimalSoldEvent.
/// Permission: animals:sell
/// </summary>
public sealed record SellAnimalCommand(
    Guid AnimalId,
    decimal SalePriceBdt,
    DateOnly SaleDate,
    string? BuyerName,
    decimal? SaleWeightKg,
    Farm360.Contracts.Finance.CreditTermsRequest? Credit = null,
    bool AllowDuringMeatWithdrawal = false,
    // The account the sale money goes into (null = the farm's default).
    Guid? MoneyAccountId = null) : IRequest, Farm360.Application.Common.Behaviors.ITransactionalCommand;

public sealed class SellAnimalCommandValidator : AbstractValidator<SellAnimalCommand>
{
    public SellAnimalCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.AnimalId).NotEmpty();

        RuleFor(x => x.SalePriceBdt)
            .GreaterThan(0).WithMessage("Sale price must be greater than zero.");

        RuleFor(x => x.SaleDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Sale date cannot be in the future.");

        RuleFor(x => x.SaleWeightKg)
            .GreaterThan(0).WithMessage("Sale weight must be greater than zero.")
            .When(x => x.SaleWeightKg.HasValue);

        RuleFor(x => x.BuyerName)
            .MaximumLength(200).WithMessage("Buyer name cannot exceed 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.BuyerName));

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Credit!.PartyName) || !string.IsNullOrWhiteSpace(x.BuyerName))
            .When(x => x.Credit is not null)
            .WithMessage("Enter the buyer's name for a sale on credit.");

        RuleFor(x => x.Credit!.PaidNowBdt)
            .GreaterThanOrEqualTo(0)
            .LessThan(x => x.SalePriceBdt)
            .When(x => x.Credit is not null)
            .WithMessage("The amount received now must be less than the sale price (otherwise it is a cash sale).");
    }
}

public sealed class SellAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    MediatR.IPublisher publisher,
    Farm360.Application.Finance.Services.ITradeCreditService? credit = null,
    Farm360.Application.Feeding.Services.IAnimalExitService? exitService = null,
    Farm360.Domain.Health.Interfaces.Repositories.IMedicalTreatmentRepository? treatments = null,
    Farm360.Application.Finance.Services.IMoneyAccountService? money = null) : IRequestHandler<SellAnimalCommand>
{
    public async Task Handle(SellAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        // Food safety: an animal still inside a medicine's meat withdrawal period must not be sold
        // for meat. The seller can override (e.g. sold live to another farm) after being told.
        if (!request.AllowDuringMeatWithdrawal && treatments is not null)
        {
            DateOnly? clearOn = null;
            string? medicine = null;
            foreach (var t in await treatments.GetByAnimalIdAsync(animal.Id, cancellationToken))
            {
                if (t.WithdrawalPeriod.MeatDays <= 0 || t.StartDate > request.SaleDate)
                    continue;
                var position = Farm360.Application.Reporting.Definitions.Health.Withdrawal.Evaluate(
                    t.StartDate, t.EndDate, t.Status, t.WithdrawalPeriod.MeatDays, t.WithdrawalPeriod.MilkDays, request.SaleDate);
                if (!position.MeatWithheld)
                    continue;
                var clear = position.MeatClearDate ?? DateOnly.MaxValue;
                if (clearOn is null || clear > clearOn)
                {
                    clearOn = clear;
                    medicine = t.MedicationName;
                }
            }

            if (clearOn is { } until)
                throw new Farm360.Domain.Exceptions.DomainException(until == DateOnly.MaxValue
                    ? $"{animal.Tag.TagId} is still on {medicine}, so its meat is not safe yet. Finish the treatment first, or confirm the sale anyway."
                    : $"{animal.Tag.TagId} is in the meat withdrawal period of {medicine} until {until:dd MMM yyyy}. Sell on or after that day, or confirm the sale anyway.");
        }

        animal.Sell(request.SalePriceBdt, request.SaleDate, currentUser.UserId ?? Guid.Empty, request.BuyerName, request.SaleWeightKg);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish event for Finance integration
        var domainEvent = animal.DomainEvents.OfType<Farm360.Domain.Livestock.Events.AnimalSoldEvent>().FirstOrDefault();
        if (domainEvent != null)
        {
            await publisher.Publish(new Farm360.Application.Finance.EventHandlers.Integration.AnimalSoldNotification(domainEvent), cancellationToken);
        }

        // Sold on credit: the buyer owes the balance (Accounts Receivable) until it is collected.
        if (request.Credit is { } terms && credit is not null)
        {
            var buyer = string.IsNullOrWhiteSpace(terms.PartyName) ? request.BuyerName! : terms.PartyName;
            await credit.OpenForSourceAsync(animal.FarmId, animal.Id, Farm360.Domain.Finance.Enums.TransactionCategory.AnimalSale,
                new Farm360.Application.Finance.Services.CreditTerms(buyer, terms.PartyPhone, terms.DueDate, terms.PaidNowBdt, terms.PaymentMethod, terms.Notes,
                    MoneyAccountId: terms.MoneyAccountId ?? request.MoneyAccountId),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (money is not null && request.MoneyAccountId is not null)
        {
            // Paid in full on the day: the money goes into the chosen account.
            await money.ApplyToSourceAsync(animal.FarmId, animal.Id, Farm360.Domain.Finance.Enums.TransactionCategory.AnimalSale,
                new Farm360.Application.Finance.Services.PaymentSource(request.MoneyAccountId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Feeding stops from the sale date: plans end, pending entries are skipped, entries
        // confirmed after the sale are reversed, group entries re-split without the animal.
        // Called directly (not via AnimalSoldEvent) because Finance also listens to that event and
        // has already booked the sale above; this runs exactly once, inside the sale's transaction.
        if (exitService is not null)
        {
            await exitService.ProcessExitAsync(animal.Id, request.SaleDate,
                Farm360.Application.Feeding.Services.AnimalExitService.SoldReason(request.SaleDate), cancellationToken);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// QUARANTINE ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Places animal under quarantine. Permission: animals:quarantine
/// </summary>
public sealed record QuarantineAnimalCommand(
    Guid AnimalId,
    string Reason) : IRequest;

public sealed class QuarantineAnimalCommandValidator : AbstractValidator<QuarantineAnimalCommand>
{
    public QuarantineAnimalCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Quarantine reason is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}

public sealed class QuarantineAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<QuarantineAnimalCommand>
{
    public async Task Handle(QuarantineAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.Quarantine(request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RELEASE FROM QUARANTINE
// ══════════════════════════════════════════════════════════════════════════════

public sealed record ReleaseFromQuarantineCommand(Guid AnimalId) : IRequest;

public sealed class ReleaseFromQuarantineCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<ReleaseFromQuarantineCommand>
{
    public async Task Handle(ReleaseFromQuarantineCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.ReleaseFromQuarantine();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RECORD DEATH
// ══════════════════════════════════════════════════════════════════════════════

public sealed record RecordAnimalDeathCommand(
    Guid AnimalId,
    DisposalReason Cause,
    DateOnly DeathDate,
    string? Notes) : IRequest, Farm360.Application.Common.Behaviors.ITransactionalCommand;

public sealed class RecordAnimalDeathCommandValidator : AbstractValidator<RecordAnimalDeathCommand>
{
    public RecordAnimalDeathCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.Cause).IsInEnum()
            .NotEqual(DisposalReason.Sale).WithMessage("Use Record Sale for a sold animal.");
        RuleFor(x => x.DeathDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Death date cannot be in the future.");
    }
}

/// <summary>
/// Records a death the same way the Health mortality screen does, so every death has a mortality
/// record (mortality list, health dashboard) as well as the animal's Dead status, and the Finance,
/// Feeding and Health exit handlers run in the same transaction.
/// </summary>
public sealed class RecordAnimalDeathCommandHandler(ISender sender) : IRequestHandler<RecordAnimalDeathCommand>
{
    public async Task Handle(RecordAnimalDeathCommand request, CancellationToken cancellationToken)
    {
        var cause = request.Cause switch
        {
            DisposalReason.Disease => Farm360.Domain.Health.Enums.CauseOfDeath.Disease,
            DisposalReason.Accident => Farm360.Domain.Health.Enums.CauseOfDeath.Accident,
            DisposalReason.NaturalDeath => Farm360.Domain.Health.Enums.CauseOfDeath.NaturalCauses,
            DisposalReason.Slaughter => Farm360.Domain.Health.Enums.CauseOfDeath.Slaughter,
            _ => Farm360.Domain.Health.Enums.CauseOfDeath.Unknown
        };

        await sender.Send(new Farm360.Application.Health.Commands.MortalityRecords.RecordMortalityCommand(
            request.AnimalId,
            request.DeathDate,
            cause,
            DiseaseName: cause == Farm360.Domain.Health.Enums.CauseOfDeath.Disease ? "Not specified" : null,
            PostMortemNotes: request.Notes,
            EstimatedEconomicLossBdt: null,
            DiseaseIncidentId: null), cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TRANSFER ANIMAL
// ══════════════════════════════════════════════════════════════════════════════

public sealed record TransferAnimalCommand(
    Guid AnimalId,
    Guid? ToShedId,
    Guid? ToPenId,
    DateOnly TransferDate,
    string? Reason) : IRequest;

public sealed class TransferAnimalCommandValidator : AbstractValidator<TransferAnimalCommand>
{
    public TransferAnimalCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.AnimalId).NotEmpty();
        
        RuleFor(x => x.TransferDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Transfer date cannot be in the future.");
            
        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.")
            .When(x => x.Reason is not null);
    }
}

public sealed class TransferAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    Farm360.Domain.Farms.Repositories.IPenRepository? pens = null) : IRequestHandler<TransferAnimalCommand>
{
    public async Task Handle(TransferAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        // Only animals on the farm can be moved (the domain refuses sold/dead animals).
        animal.EnsureOnFarm("a pen/shed transfer");

        // Capacity: a pen is full when its on-farm occupancy reaches capacity (0 = no limit).
        // Occupancy counts only Active/Quarantined animals with an open movement, so animals that
        // were sold or died never block a transfer.
        if (request.ToPenId is { } penId && pens is not null && animal.CurrentMovement?.PenId != penId)
        {
            var pen = await pens.GetByIdAsync(animal.TenantId, penId, cancellationToken);
            if (pen is not null && pen.Capacity > 0)
            {
                var occupancy = await pens.GetOccupancyByPenAsync(animal.TenantId, penId, cancellationToken);
                if (occupancy >= pen.Capacity)
                    throw new Farm360.Domain.Livestock.Exceptions.PenFullException(pen.PenName, pen.Capacity);
            }
        }

        animal.TransferToShed(request.ToShedId, request.ToPenId, request.TransferDate, currentUser.UserId ?? Guid.Empty, request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DELETE ANIMAL (soft delete)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Soft-deletes an animal. Constitution §12: No hard deletes.
/// Permission: animals:delete
/// </summary>
public sealed record DeleteAnimalCommand(Guid AnimalId) : IRequest;

public sealed class DeleteAnimalCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    Farm360.Application.Finance.Services.ILedgerPostingService ledgerPosting) : IRequestHandler<DeleteAnimalCommand>
{
    public async Task Handle(DeleteAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        repository.SoftDelete(animal, currentUser.UserId ?? Guid.Empty);

        // A deleted animal was a registration mistake: its purchase, sale and disposal postings go
        // with it so the livestock asset does not keep an animal that never existed. Feed and
        // medicine already consumed stay — that stock really was used.
        foreach (var category in new[]
                 {
                     Farm360.Domain.Finance.Enums.TransactionCategory.AnimalPurchase,
                     Farm360.Domain.Finance.Enums.TransactionCategory.AnimalSale,
                     Farm360.Domain.Finance.Enums.TransactionCategory.CostOfAnimalsSold,
                     Farm360.Domain.Finance.Enums.TransactionCategory.LivestockLoss,
                 })
        {
            await ledgerPosting.ReverseAsync(animal.FarmId, animal.Id, category, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ADD PHOTO
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Adds a photo URL to the animal after the client has uploaded to S3.
/// The API returns a presigned upload URL; the client uploads directly; then calls this command.
/// Permission: animals:write
/// </summary>
public sealed record AddAnimalPhotoCommand(
    Guid AnimalId,
    string PhotoUrl,
    string? Caption) : IRequest<AnimalPhotoDto>;

public sealed class AddAnimalPhotoCommandValidator : AbstractValidator<AddAnimalPhotoCommand>
{
    public AddAnimalPhotoCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.PhotoUrl)
            .NotEmpty().WithMessage("Photo URL is required.")
            .MaximumLength(1000).WithMessage("Photo URL cannot exceed 1000 characters.");
        RuleFor(x => x.Caption)
            .MaximumLength(200)
            .When(x => x.Caption is not null);
    }
}

public sealed class AddAnimalPhotoCommandHandler(
    IAnimalRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<AddAnimalPhotoCommand, AnimalPhotoDto>
{
    public async Task<AnimalPhotoDto> Handle(AddAnimalPhotoCommand request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var photo = animal.AddPhoto(request.PhotoUrl, request.Caption, currentUser.UserId ?? Guid.Empty);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return photo.ToDto();
    }
}
