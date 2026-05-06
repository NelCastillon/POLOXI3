using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Engagements;

public sealed class CreateEngagementMilestoneRequest
{
    public Guid TenantId { get; set; }
    [Required]
    public Guid EngagementId { get; set; }
    [Required, StringLength(200)]
    public string MilestoneName { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
