using Farm360.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Farm360.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment env, ILogger<LocalFileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string directory, CancellationToken cancellationToken = default)
    {
        if (fileStream == null || fileStream.Length == 0)
            throw new ArgumentException("File stream is empty.", nameof(fileStream));

        // Generate a unique filename to prevent collisions
        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var relativePath = Path.Combine("uploads", directory, uniqueFileName).Replace("\\", "/");
        
        var targetDirectory = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", directory);

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var fullPath = Path.Combine(targetDirectory, uniqueFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
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

        // Ensure we only delete from our wwwroot
        var relativePath = filePath.TrimStart('/');
        var fullPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), relativePath);

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
