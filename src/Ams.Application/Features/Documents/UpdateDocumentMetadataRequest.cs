using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Documents;

public sealed class UpdateDocumentMetadataRequest
{
    public Guid DocumentId { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Tags { get; set; }
    public DateOnly? RetentionDate { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
