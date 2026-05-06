using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateTaskItemRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string TaskNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string TaskTypeCode { get; set; } = "Task";

    [Required]
    [StringLength(50)]
    public string StageCode { get; set; } = "Intake";

    [Required]
    [StringLength(50)]
    public string PriorityCode { get; set; } = "Medium";

    public string StatusCode { get; set; } = "Open";
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateTaskItemRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string TaskTypeCode { get; set; } = "Task";

    [Required]
    [StringLength(50)]
    public string StageCode { get; set; } = "Intake";

    [Required]
    [StringLength(50)]
    public string PriorityCode { get; set; } = "Medium";

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Open";

    public Guid? AssignedToUserId { get; set; }

    [StringLength(100)]
    public string? RelatedEntityName { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
