namespace Ams.Domain.Entities;

public sealed class DocumentVersion
{
    public Guid DocumentVersionId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string? ContentType { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string? ChangeNotes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private DocumentVersion() { }

    public DocumentVersion(Guid tenantId, Guid documentId, int versionNumber, string fileName, string storagePath, string? contentType, long? fileSizeBytes, string? changeNotes, Guid? createdByUserId)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        VersionNumber = versionNumber;
        FileName = fileName;
        StoragePath = storagePath;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        ChangeNotes = changeNotes;
        CreatedByUserId = createdByUserId;
    }
}
