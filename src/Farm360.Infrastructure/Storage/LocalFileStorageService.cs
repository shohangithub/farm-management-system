using Farm360.Application.Common.Files;
using Farm360.Application.Common.Interfaces;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Farm360.Infrastructure.Storage;

/// <summary>
/// Stores uploads under wwwroot/uploads/{directory}. Files there are served anonymously, so this is
/// the single choke point that guarantees only validated images are ever written: the stored name
/// is a server-generated GUID plus an extension derived from the file's magic bytes; the client
/// file name never contributes a path segment.
/// </summary>
public partial class LocalFileStorageService : IFileStorageService
{
    [GeneratedRegex("^[a-z0-9-]{1,40}$")]
    private static partial Regex DirectoryNamePattern();

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string directory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (string.IsNullOrWhiteSpace(directory) || !DirectoryNamePattern().IsMatch(directory))
            throw new ArgumentException("Invalid upload directory.", nameof(directory));

        // Content-based validation (size, extension whitelist, magic bytes). The extension of the
        // stored file comes from the detected format, never from the client.
        var extension = ImageUploadValidator.Validate(fileStream, fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine("uploads", directory, uniqueFileName).Replace("\\", "/");
        
        var targetDirectory = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", directory);

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var fullPath = Path.Combine(targetDirectory, uniqueFileName);

        using (var stream = new FileStream(fullPath, FileMode.CreateNew))
        {
            await fileStream.CopyToAsync(stream, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("File uploaded to {Path}", fullPath);
        }

        // Return relative URL
        return $"/{relativePath}";
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.CompletedTask;

        // Ensure we only delete from wwwroot/uploads (no traversal outside it)
        var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads"));
        var relativePath = filePath.TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), relativePath));
        if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Refused to delete file outside the uploads folder: {Path}", filePath);
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted file {Path}", fullPath);
            }
        }

        return Task.CompletedTask;
    }
}
