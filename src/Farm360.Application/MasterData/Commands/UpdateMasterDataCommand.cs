using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.MasterData.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.MasterData.Commands;

public record UpdateMasterDataCommand(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive) : IRequest, ITransactionalCommand;

public class UpdateMasterDataCommandValidator : AbstractValidator<UpdateMasterDataCommand>
{
    public UpdateMasterDataCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(500);
    }
}

public class UpdateMasterDataCommandHandler : IRequestHandler<UpdateMasterDataCommand>
{
    private readonly IMasterDataRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMasterDataCommandHandler(
        IMasterDataRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateMasterDataCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var entry = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.MasterData.MasterDataEntry), request.Id);

        entry.UpdateDetails(
            request.Name,
            request.Description,
            request.DisplayOrder,
            request.IsActive);

        _repository.Update(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
