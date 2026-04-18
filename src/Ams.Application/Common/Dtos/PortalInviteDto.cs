namespace Ams.Application.Common.Dtos;

public sealed class PortalInviteDto
{
    public Guid PortalInviteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string InviteEmail { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? SentDateUtc { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public DateTime? AcceptedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
