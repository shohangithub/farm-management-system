using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Farms.Pens.Commands;

public record CreatePenCommand(
    Guid ShedId,
    string PenNumber,
    string PenName,
    int Capacity,
    string? AnimalGroup,
    string? Notes) : IRequest<Guid>;

public class CreatePenCommandValidator : AbstractValidator<CreatePenCommand>
{
    public CreatePenCommandValidator()
    {
        RuleFor(v => v.ShedId).NotEmpty();
        RuleFor(v => v.PenNumber).NotEmpty().MaximumLength(50);
        RuleFor(v => v.PenName).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Capacity).GreaterThanOrEqualTo(0);
        RuleFor(v => v.AnimalGroup).MaximumLength(100);
        RuleFor(v => v.Notes).MaximumLength(500);
    }
}

public class CreatePenCommandHandler : IRequestHandler<CreatePenCommand, Guid>
{
    private readonly IPenRepository _penRepository;
    private readonly IShedRepository _shedRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePenCommandHandler(
        IPenRepository penRepository,
        IShedRepository shedRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _penRepository = penRepository;
        _shedRepository = shedRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreatePenCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var shed = await _shedRepository.GetByIdAsync(tenantId, request.ShedId, cancellationToken)
            ?? throw new Common.Exceptions.ValidationException(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("ShedId", "Shed not found.") 
            });

        if (await _penRepository.ExistsByNumberAsync(tenantId, request.ShedId, request.PenNumber, cancellationToken))
        {
            throw new Common.Exceptions.ValidationException(new[] 
            { 
                new FluentValidation.Results.ValidationFailure("PenNumber", "The specified Pen Number already exists in this shed.") 
            });
        }

        var pen = Pen.Create(
            tenantId,
            request.ShedId,
            request.PenNumber,
            request.PenName,
            request.Capacity,
            request.AnimalGroup,
            request.Notes);

        _penRepository.Add(pen);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return pen.Id;
    }
}
