namespace Ams.Application.Features.Documents;

public sealed class DeleteDocumentRequest
{
    public Guid DocumentId { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
