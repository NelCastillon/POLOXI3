namespace Ams.Application.Features.Iam;

public sealed class AssignUserScopeRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string ScopeTypeCode { get; set; } = string.Empty;
    public string ScopeValue { get; set; } = string.Empty;
    public DateTime? ExpiresDateUtc { get; set; }
    public Guid? GrantedByUserId { get; set; }
}
