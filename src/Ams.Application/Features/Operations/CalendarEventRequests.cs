using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateCalendarEventRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required, StringLength(50)]
    public string EventTypeCode { get; set; } = "Event";

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Scheduled";

    public DateTime StartDateTimeUtc { get; set; }
    public DateTime? EndDateTimeUtc { get; set; }
    public bool AllDay { get; set; }

    [Required, StringLength(100)]
    public string TimeZoneId { get; set; } = "America/Chicago";

    public Guid? OrganizerUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }

    [StringLength(50)]
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCalendarEventRequest
{
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required, StringLength(50)]
    public string EventTypeCode { get; set; } = "Event";

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Scheduled";

    public DateTime StartDateTimeUtc { get; set; }
    public DateTime? EndDateTimeUtc { get; set; }
    public bool AllDay { get; set; }

    [Required, StringLength(100)]
    public string TimeZoneId { get; set; } = "America/Chicago";

    public Guid? OrganizerUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }

    [StringLength(50)]
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
