namespace Ams.Application.Features.Iam;

public sealed class GrantUserPermissionRequest
{
    public Guid      TenantId              { get; set; }
    public Guid      UserId                { get; set; }
    public Guid      PermissionId          { get; set; }
    public bool      IsGranted             { get; set; } = true;
    public DateTime? EffectiveStartDateUtc { get; set; }
    public DateTime? ExpiresDateUtc        { get; set; }
    public Guid?     GrantedByUserId       { get; set; }
    public string?   Reason                { get; set; }
    public Guid?     ApprovedByUserId      { get; set; }
}
