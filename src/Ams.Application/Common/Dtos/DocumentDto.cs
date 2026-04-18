namespace Ams.Application.Common.Dtos;

public sealed class DocumentDto
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public int VersionNumber { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateOnly? RetentionDate { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
