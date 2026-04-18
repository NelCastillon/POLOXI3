namespace Ams.Domain.Entities;

public sealed class LeadActivity
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public string ActivityTypeCode { get; set; } = "Note";
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateOnly ActivityDate { get; set; }
    public int? DurationMinutes { get; set; }
    public string? OutcomeCode { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public bool IsDeleted { get; set; }
}
