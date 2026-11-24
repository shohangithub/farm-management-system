namespace Farm360.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string directory, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads back a file this service stored (a relative URL such as
    /// <c>/uploads/organizations/abc.png</c>). Returns null — never throws — when the path is outside
    /// the uploads folder, the file is missing or unreadable, or it is larger than
    /// <paramref name="maxBytes"/>: a missing logo must not stop a report or an invoice printing.
    /// </summary>
    Task<byte[]?> ReadFileAsync(string filePath, long maxBytes = 2 * 1024 * 1024, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(null);
}
