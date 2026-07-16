using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.MasterData;
using Farm360.Domain.MasterData.Enums;
using Farm360.Domain.MasterData.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.MasterData.Commands;

public record CreateMasterDataCommand(
    int Type,
    string Name,
    string Code,
    string? Description,
    int DisplayOrder) : IRequest<Guid>;

public class CreateMasterDataCommandValidator : AbstractValidator<CreateMasterDataCommand>
{
    public CreateMasterDataCommandValidator()
    {
        RuleFor(v => v.Type).InclusiveBetween(1, 14);
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Code).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Description).MaximumLength(500);
    }
}

public class CreateMasterDataCommandHandler : IRequestHandler<CreateMasterDataCommand, Guid>
{
    private readonly IMasterDataRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMasterDataCommandHandler(
        IMasterDataRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateMasterDataCommand request, CancellationToken cancellationToken)
    {
        var type = (MasterDataType)request.Type;
        var tenantId = _tenantService.TenantId;

        if (await _repository.ExistsByCodeAsync(tenantId, type, request.Code, cancellationToken))
        {
            throw new Common.Exceptions.ValidationException(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("Code", $"The code '{request.Code}' already exists for this master data type.") 
            });
        }

        var entry = MasterDataEntry.Create(
            tenantId,
            type,
            request.Name,
            request.Code,
            request.Description,
            request.DisplayOrder);

        _repository.Add(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }
}
