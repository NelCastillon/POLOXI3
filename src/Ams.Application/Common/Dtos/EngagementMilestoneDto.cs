namespace Ams.Application.Common.Dtos;

public sealed class EngagementMilestoneDto
{
    public Guid MilestoneId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EngagementId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
