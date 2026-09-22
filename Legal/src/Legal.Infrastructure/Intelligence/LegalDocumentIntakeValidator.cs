using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class LegalDocumentIntakeValidator(IOptions<DocumentIntelligenceOptions> options) : ILegalDocumentIntakeValidator
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".txt"] = ["text/plain"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".pptx"] = ["application/vnd.openxmlformats-officedocument.presentationml.presentation"],
        [".html"] = ["text/html"],
        [".htm"] = ["text/html"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".bmp"] = ["image/bmp"],
        [".tif"] = ["image/tiff"],
        [".tiff"] = ["image/tiff"]
    };

    public async Task ValidateAsync(LegalDocumentIntakeRequest request, Stream content, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(request.FileName);
        if (!string.Equals(fileName, request.FileName, StringComparison.Ordinal) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("The document filename is invalid.");
        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > options.Value.MaximumFileSizeBytes)
            throw new InvalidDataException("The document size is outside the configured intake limit.");

        var extension = Path.GetExtension(fileName);
        if (!AllowedTypes.TryGetValue(extension, out var mediaTypes) || !mediaTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("The document extension and media type are not an allowed combination.");
        if (!content.CanSeek)
            throw new InvalidDataException("Document validation requires a seekable stream.");

        var header = new byte[Math.Min(8, checked((int)content.Length))];
        content.Position = 0;
        _ = await content.ReadAsync(header, cancellationToken);
        content.Position = 0;
        if (!SignatureMatches(extension, header))
            throw new InvalidDataException("The document content signature does not match its extension.");
    }

    private static bool SignatureMatches(string extension, ReadOnlySpan<byte> header) => extension.ToLowerInvariant() switch
    {
        ".pdf" => header.StartsWith("%PDF-"u8),
        ".docx" or ".xlsx" or ".pptx" => header.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
        ".jpg" or ".jpeg" => header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        ".png" => header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".bmp" => header.StartsWith("BM"u8),
        ".tif" or ".tiff" => header.StartsWith("II*\0"u8) || header.StartsWith("MM\0*"u8),
        ".txt" or ".html" or ".htm" => !header.Contains((byte)0),
        _ => false
    };
}