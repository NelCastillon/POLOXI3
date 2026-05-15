using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateTaskTypeRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string TaskTypeCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string TaskTypeName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, 9999)]
    public int SortOrder { get; set; } = 100;

    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateTaskTypeRequest
{
    [Required]
    [StringLength(50)]
    public string TaskTypeCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string TaskTypeName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, 9999)]
    public int SortOrder { get; set; } = 100;

    public bool IsActive { get; set; } = true;
    public Guid? ModifiedByUserId { get; set; }
}
