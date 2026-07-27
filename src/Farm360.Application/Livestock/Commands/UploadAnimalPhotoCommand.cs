using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;
using System.Text.Json.Serialization;

namespace Farm360.Application.Livestock.Commands;

/// <summary>
/// Handles multipart/form-data upload of an animal photo.
/// Constitution §3.2 CQRS Pattern: Commands modify state.
/// </summary>
public sealed record UploadAnimalPhotoCommand(
    Guid AnimalId, 
    Stream FileStream,
    string FileName,
    string ContentType,
    string? Caption, 
    bool IsPrimary = false) : IRequest<AnimalPhotoDto>;

public sealed class UploadAnimalPhotoCommandValidator : AbstractValidator<UploadAnimalPhotoCommand>
{
    public UploadAnimalPhotoCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");
            
        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.");
            
        RuleFor(x => x.Caption)
            .MaximumLength(200).WithMessage("Caption cannot exceed 200 characters.")
            .When(x => x.Caption is not null);
    }
}

public sealed class UploadAnimalPhotoCommandHandler : IRequestHandler<UploadAnimalPhotoCommand, AnimalPhotoDto>
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public UploadAnimalPhotoCommandHandler(
        IAnimalRepository animalRepository,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _animalRepository = animalRepository;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AnimalPhotoDto> Handle(UploadAnimalPhotoCommand request, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        if (request.FileStream == null || request.FileStream.Length == 0)
        {
            throw new ArgumentException("File stream is empty.");
        }

        // Validate size (e.g. max 5MB)
        if (request.FileStream.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("File size exceeds the 5MB limit.");
        }

        // Validate type
        var contentType = request.ContentType.ToLowerInvariant();
        if (contentType != "image/jpeg" && contentType != "image/png" && contentType != "image/webp")
        {
            throw new ArgumentException("Only JPEG, PNG and WebP images are supported.");
        }

        string photoUrl = await _fileStorageService.UploadFileAsync(request.FileStream, request.FileName, "animals", cancellationToken);

        var photo = animal.AddPhoto(photoUrl, request.Caption, Guid.Empty);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AnimalPhotoDto(
            photo.Id,
            photo.AnimalId,
            photo.PhotoUrl,
            photo.Caption,
            photo.IsPrimary,
            photo.UploadedAtUtc);
    }
}
