namespace Ams.Application.Common.Dtos;

public sealed class RetentionPolicyDto
{
    public Guid RetentionPolicyId { get; set; }
    public Guid TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public DateTime? LastAppliedDateUtc { get; set; }
    public int? LastAppliedCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
