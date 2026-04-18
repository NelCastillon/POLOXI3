namespace Ams.Application.Features.Documents;

public sealed class CreateDocumentVersionRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ChangeNotes { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
