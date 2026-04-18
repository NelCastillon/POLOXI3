namespace Ams.Application.Common.Dtos;

public sealed class DocumentAccessLogDto
{
    public Guid AccessLogId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ShareLinkId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime AccessDateUtc { get; set; }
}
