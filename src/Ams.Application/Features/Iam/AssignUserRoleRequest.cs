namespace Ams.Application.Features.Iam;

public sealed class AssignUserRoleRequest
{
    public Guid      TenantId              { get; set; }
    public Guid      UserId                { get; set; }
    public Guid      RoleId                { get; set; }
    public DateTime? EffectiveStartDateUtc { get; set; }
    public DateTime? EffectiveEndDateUtc   { get; set; }
    public Guid?     AssignedByUserId      { get; set; }
    public string?   Source                { get; set; }
    public string?   Reason                { get; set; }
    public Guid?     ApproverId            { get; set; }
    public string?   ScopeTypeCode         { get; set; }
    public string?   ScopeValue            { get; set; }
}
