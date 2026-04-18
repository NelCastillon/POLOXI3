namespace Ams.Application.Features.Iam;

public sealed class CreateFieldSecurityPolicyRequest
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public bool CanRead { get; set; } = true;
    public bool CanWrite { get; set; }
    public bool IsHidden { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
