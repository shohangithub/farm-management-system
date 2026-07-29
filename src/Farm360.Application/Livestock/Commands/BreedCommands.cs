using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Livestock.Commands;

public sealed record CreateBreedCommand(
    string Name,
    string Description,
    string Category,
    string Origin,
    string MainPurpose,
    string BestFor,
    decimal AdgPoorManagement,
    decimal AdgAverageFarm,
    decimal AdgGoodCommercialFarm,
    decimal AdgIntensiveFattening,
    decimal StandardAdgMin,
    decimal StandardAdgMax,
    decimal FcrMin,
    decimal FcrMax,
    decimal MilkYieldMinLiters,
    decimal MilkYieldMaxLiters,
    decimal FatPercentageMin,
    decimal FatPercentageMax) : IRequest<BreedDto>;

public sealed class CreateBreedCommandValidator : AbstractValidator<CreateBreedCommand>
{
    public CreateBreedCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MainPurpose).NotEmpty().MaximumLength(50);
    }
}

public sealed class CreateBreedCommandHandler(
    IBreedRepository repository,
    IUnitOfWork unitOfWork,
    ITenantService tenantService) : IRequestHandler<CreateBreedCommand, BreedDto>
{
    public async Task<BreedDto> Handle(CreateBreedCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Breed with name '{request.Name}' already exists.");

        var breed = new Breed(
            Guid.NewGuid(),
            tenantService.TenantId,
            request.Name,
            request.Description ?? "",
            request.Category,
            request.Origin ?? "",
            request.MainPurpose,
            request.AdgPoorManagement,
            request.AdgAverageFarm,
            request.AdgGoodCommercialFarm,
            request.AdgIntensiveFattening,
            request.FcrMin,
            request.FcrMax,
            request.StandardAdgMin,
            request.StandardAdgMax,
            request.MilkYieldMinLiters,
            request.MilkYieldMaxLiters,
            request.FatPercentageMin,
            request.FatPercentageMax,
            request.BestFor ?? "");

        repository.Add(breed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return breed.ToDto();
    }
}

public sealed record UpdateBreedCommand(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string Origin,
    string MainPurpose,
    string BestFor,
    decimal AdgPoorManagement,
    decimal AdgAverageFarm,
    decimal AdgGoodCommercialFarm,
    decimal AdgIntensiveFattening,
    decimal StandardAdgMin,
    decimal StandardAdgMax,
    decimal FcrMin,
    decimal FcrMax,
    decimal MilkYieldMinLiters,
    decimal MilkYieldMaxLiters,
    decimal FatPercentageMin,
    decimal FatPercentageMax) : IRequest<BreedDto>;

public sealed class UpdateBreedCommandValidator : AbstractValidator<UpdateBreedCommand>
{
    public UpdateBreedCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MainPurpose).NotEmpty().MaximumLength(50);
    }
}

public sealed class UpdateBreedCommandHandler(
    IBreedRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateBreedCommand, BreedDto>
{
    public async Task<BreedDto> Handle(UpdateBreedCommand request, CancellationToken cancellationToken)
    {
        var breed = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Breed), request.Id);

        breed.UpdateDetails(request.Name, request.Description ?? "", request.Category, request.Origin ?? "", request.MainPurpose, request.BestFor ?? "");
        breed.UpdateGrowthMetrics(request.AdgPoorManagement, request.AdgAverageFarm, request.AdgGoodCommercialFarm, request.AdgIntensiveFattening, request.StandardAdgMin, request.StandardAdgMax);
        breed.UpdateEfficiencyMetrics(request.FcrMin, request.FcrMax);
        breed.UpdateDairyMetrics(request.MilkYieldMinLiters, request.MilkYieldMaxLiters, request.FatPercentageMin, request.FatPercentageMax);

        repository.Update(breed);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return breed.ToDto();
    }
}

public sealed record DeleteBreedCommand(Guid Id) : IRequest;

public sealed class DeleteBreedCommandHandler(
    IBreedRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteBreedCommand>
{
    public async Task Handle(DeleteBreedCommand request, CancellationToken cancellationToken)
    {
        var breed = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Breed), request.Id);

        repository.Delete(breed);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
