namespace Ams.Application.Features.Documents;

public sealed class UpdateDocumentMetadataRequest
{
    public Guid DocumentId { get; set; }
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public DateOnly? RetentionDate { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
