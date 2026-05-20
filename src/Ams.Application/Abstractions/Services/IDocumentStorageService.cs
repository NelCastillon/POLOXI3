namespace Ams.Application.Abstractions.Services;

public interface IDocumentStorageService
{
    Task<DocumentStorageUploadResult> UploadAsync(DocumentStorageUploadRequest request, CancellationToken cancellationToken = default);
    Task<DocumentStorageDownloadResult?> DownloadAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}

public sealed class DocumentStorageUploadRequest
{
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Stream Content { get; set; } = Stream.Null;
    public string? ExistingStoragePath { get; set; }
}

public sealed class DocumentStorageUploadResult
{
    public string StoragePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ContentType { get; set; }
}

public sealed class DocumentStorageDownloadResult
{
    public Stream Content { get; set; } = Stream.Null;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
}
