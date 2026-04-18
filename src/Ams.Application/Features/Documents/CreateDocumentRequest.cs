namespace Ams.Application.Features.Documents;

public sealed class CreateDocumentRequest
{
    public Guid TenantId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = "Other";
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public DateOnly? RetentionDate { get; set; }
    public string? UploadedByName { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
