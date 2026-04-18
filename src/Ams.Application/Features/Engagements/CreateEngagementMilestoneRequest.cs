namespace Ams.Application.Features.Engagements;

public sealed class CreateEngagementMilestoneRequest
{
    public Guid TenantId { get; set; }
    public Guid EngagementId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
