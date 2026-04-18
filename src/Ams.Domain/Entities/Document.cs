using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Document : AuditableEntity
{
    public string DocumentTypeCode { get; private set; } = string.Empty;
    public DocumentCategory Category { get; private set; } = DocumentCategory.Other;
    public string? EntityName { get; private set; }
    public Guid? EntityId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string? ContentType { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public int VersionNumber { get; private set; } = 1;
    public DocumentStatus Status { get; private set; } = DocumentStatus.Active;
    public DateOnly? RetentionDate { get; private set; }
    public string? Description { get; private set; }
    public string? Tags { get; private set; }
    public string? UploadedByName { get; private set; }

    private Document() { }

    public Document(Guid tenantId, string documentTypeCode, DocumentCategory category, string fileName, string storagePath,
        string? contentType, long? fileSizeBytes, string? entityName, Guid? entityId,
        string? description, string? tags, DateOnly? retentionDate, string? uploadedByName, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        DocumentTypeCode = documentTypeCode;
        Category = category;
        FileName = fileName;
        StoragePath = storagePath;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        EntityName = entityName;
        EntityId = entityId;
        Description = description;
        Tags = tags;
        RetentionDate = retentionDate;
        UploadedByName = uploadedByName;
        Status = DocumentStatus.Active;
    }

    public void UpdateMetadata(string? description, string? tags, DateOnly? retentionDate, Guid? modifiedByUserId)
    {
        Description = description;
        Tags = tags;
        RetentionDate = retentionDate;
        MarkModified(modifiedByUserId);
    }

    public void Rename(string newFileName, Guid? modifiedByUserId)
    {
        FileName = newFileName;
        MarkModified(modifiedByUserId);
    }

    public void IncrementVersion()
    {
        VersionNumber++;
    }

    public void Archive(Guid? modifiedByUserId)
    {
        Status = DocumentStatus.Archived;
        MarkModified(modifiedByUserId);
    }
}
