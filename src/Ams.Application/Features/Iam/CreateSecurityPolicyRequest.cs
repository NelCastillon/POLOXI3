namespace Ams.Application.Features.Iam;

public sealed class CreateSecurityPolicyRequest
{
    public Guid TenantId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ResourceCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string ConditionExpression { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = "Block";
    public string? ErrorMessageTemplate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
