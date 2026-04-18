namespace Ams.Application.Common.Dtos;

public sealed class LeadActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public Guid? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public string ActivityTypeCode { get; set; } = "Note";
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ActivityDate { get; set; }
    public int? DurationMinutes { get; set; }
    public string? OutcomeCode { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
