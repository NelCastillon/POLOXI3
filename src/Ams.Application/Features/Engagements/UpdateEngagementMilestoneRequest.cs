using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Engagements;

public sealed class UpdateEngagementMilestoneRequest
{
    [Required]
    public Guid EngagementId { get; set; }

    [Required]
    [StringLength(200)]
    public string MilestoneName { get; set; } = string.Empty;

    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Pending";

    public Guid? ModifiedByUserId { get; set; }
}
