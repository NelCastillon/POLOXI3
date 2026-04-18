namespace Ams.Application.Features.Audit;

public sealed class UpdateRetentionPolicyRequest
{
    public Guid RetentionPolicyId { get; set; }
    public int RetentionDays { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
