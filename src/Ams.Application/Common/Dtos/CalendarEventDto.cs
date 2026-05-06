namespace Ams.Application.Common.Dtos;

public sealed class CalendarEventDto
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime? EndDateTimeUtc { get; set; }
    public bool AllDay { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public Guid? OrganizerUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
