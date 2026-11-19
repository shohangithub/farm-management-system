namespace Farm360.Application.Common.Files;

/// <summary>
/// Validates user-uploaded images by content, never by what the client claims.
/// Only JPEG, PNG and WebP are accepted (no SVG/HTML or anything else a browser could execute).
/// The returned extension is derived from the detected format and is the only part of the
/// upload that may influence the stored file name.
/// </summary>
public static class ImageUploadValidator
{
    /// <summary>Default upper bound for an uploaded image (5 MB).</summary>
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedClientExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    /// <summary>
    /// Checks size, client file extension and magic bytes. Throws <see cref="ArgumentException"/>
    /// with a user-facing message when the upload is not an acceptable image. Leaves a seekable
    /// stream positioned at the start.
    /// </summary>
    /// <returns>The canonical extension for the detected format (".jpg", ".png" or ".webp").</returns>
    public static string Validate(Stream stream, string? clientFileName, long maxBytes = DefaultMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("The uploaded file could not be read.");

        if (stream.Length == 0)
            throw new ArgumentException("The uploaded file is empty.");
        if (stream.Length > maxBytes)
            throw new ArgumentException($"The file exceeds the {maxBytes / (1024 * 1024)} MB limit.");

        var clientExtension = Path.GetExtension(clientFileName ?? string.Empty);
        if (string.IsNullOrEmpty(clientExtension) || !AllowedClientExtensions.Contains(clientExtension))
            throw new ArgumentException("Only JPEG, PNG and WebP images are supported.");

        var header = new byte[12];
        var read = ReadHeader(stream, header);
        var detected = DetectExtension(header.AsSpan(0, read))
            ?? throw new ArgumentException("The file content is not a valid JPEG, PNG or WebP image.");

        return detected;
    }

    /// <summary>Returns ".jpg", ".png" or ".webp" for a recognised image header, otherwise null.</summary>
    public static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";

        // WebP: "RIFF" ???? "WEBP"
        if (header.Length >= 12 &&
            header[..4].SequenceEqual("RIFF"u8) &&
            header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return ".webp";

        return null;
    }

    /// <summary>Content type for a canonical image extension returned by <see cref="Validate"/>.</summary>
    public static string ContentTypeFor(string extension) => extension switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => throw new ArgumentException("Unsupported image type.", nameof(extension)),
    };

    private static int ReadHeader(Stream stream, byte[] buffer)
    {
        var start = stream.Position;
        var total = 0;
        while (total < buffer.Length)
        {
            var n = stream.Read(buffer, total, buffer.Length - total);
            if (n == 0) break;
            total += n;
        }

        stream.Position = start;
        return total;
    }
}
