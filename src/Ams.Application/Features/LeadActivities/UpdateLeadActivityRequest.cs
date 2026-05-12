using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.LeadActivities;

public sealed class UpdateLeadActivityRequest
{
    public Guid ActivityId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    [Required, StringLength(50)]
    public string ActivityTypeCode { get; set; } = "Note";
    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    [Range(0, 1440)]
    public int? DurationMinutes { get; set; }
    [StringLength(50)]
    public string? OutcomeCode { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
