namespace Ams.Application.Features.Documents;

public sealed class RenameDocumentRequest
{
    public Guid DocumentId { get; set; }
    public string NewFileName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}
