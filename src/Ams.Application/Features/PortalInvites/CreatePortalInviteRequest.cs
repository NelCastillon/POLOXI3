namespace Ams.Application.Features.PortalInvites;

public sealed class CreatePortalInviteRequest
{
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public Guid AccountId { get; set; }
    public string InviteEmail { get; set; } = string.Empty;
    public DateTime ExpiresDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
