namespace Ams.Application.Features.LeadActivities;

public sealed class CreateLeadActivityRequest
{
    public Guid TenantId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public string ActivityTypeCode { get; set; } = "Note";
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public int? DurationMinutes { get; set; }
    public string? OutcomeCode { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
