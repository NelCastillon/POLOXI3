using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Documents;

public sealed class CreateDocumentVersionRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Required, StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [StringLength(150)]
    public string? ContentType { get; set; }

    [Range(1, 104_857_600)]
    public long? FileSizeBytes { get; set; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public string? ChangeNotes { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
