namespace Ams.Application.Features.Documents;

public sealed class CreateDocumentShareLinkRequest
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public int? MaxAccessCount { get; set; }
    public bool RequiresPin { get; set; }
    public string? Pin { get; set; }
}
