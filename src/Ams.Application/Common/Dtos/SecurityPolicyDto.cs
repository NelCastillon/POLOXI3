namespace Ams.Application.Common.Dtos;

public sealed class SecurityPolicyDto
{
    public Guid SecurityPolicyId { get; set; }
    public Guid TenantId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ResourceCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string ConditionExpression { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public string? ErrorMessageTemplate { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemPolicy { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
