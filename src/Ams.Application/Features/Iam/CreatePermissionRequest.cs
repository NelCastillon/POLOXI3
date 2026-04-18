namespace Ams.Application.Features.Iam;

public sealed class CreatePermissionRequest
{
    public Guid TenantId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
