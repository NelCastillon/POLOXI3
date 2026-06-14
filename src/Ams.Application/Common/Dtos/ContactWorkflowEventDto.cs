using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Common.Dtos;

public sealed class ContactWorkflowEventDto
{
    public Guid WorkflowEventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    [Required, StringLength(50)]
    public string EventType { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string EventTitle { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? EventDetail { get; set; }
    [StringLength(100)]
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime EventDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
