namespace Ams.Application.Features.Audit;

public sealed class CreateRetentionPolicyRequest
{
    public Guid TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
