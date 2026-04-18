namespace Ams.Application.Common.Dtos;

public sealed class DocumentVersionDto
{
    public Guid DocumentVersionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ChangeNotes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
