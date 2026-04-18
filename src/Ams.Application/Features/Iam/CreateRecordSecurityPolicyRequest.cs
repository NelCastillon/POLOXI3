namespace Ams.Application.Features.Iam;

public sealed class CreateRecordSecurityPolicyRequest
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string PolicyTypeCode { get; set; } = "Owner";
    public string? FilterExpression { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
