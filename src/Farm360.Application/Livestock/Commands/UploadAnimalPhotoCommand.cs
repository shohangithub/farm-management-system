using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock.Repositories;
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
            ?? throw new KeyNotFoundException($"Animal {request.AnimalId} not found.");

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
