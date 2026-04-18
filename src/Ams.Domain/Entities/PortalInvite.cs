namespace Ams.Domain.Entities;

public sealed class PortalInvite
{
    public Guid PortalInviteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public Guid AccountId { get; set; }
    public string InviteToken { get; set; } = string.Empty;
    public string InviteEmail { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "Pending";
    public DateTime? SentDateUtc { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public DateTime? AcceptedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public bool IsDeleted { get; set; }
}
