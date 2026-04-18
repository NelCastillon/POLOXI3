namespace Ams.Application.Features.Iam;

public sealed class AddPermissionScopeRequest
{
    public Guid   UserPermissionId { get; set; }
    public string ScopeTypeCode    { get; set; } = string.Empty;
    public string ScopeValue       { get; set; } = string.Empty;
    public Guid?  CreatedByUserId  { get; set; }
}
