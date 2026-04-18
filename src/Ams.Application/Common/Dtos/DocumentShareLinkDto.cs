namespace Ams.Application.Common.Dtos;

public sealed class DocumentShareLinkDto
{
    public Guid ShareLinkId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public int? MaxAccessCount { get; set; }
    public int AccessCount { get; set; }
    public bool RequiresPin { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
