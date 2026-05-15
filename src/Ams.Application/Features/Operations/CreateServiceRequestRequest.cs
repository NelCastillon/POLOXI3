using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateServiceRequestRequest
{
    public Guid TenantId { get; set; }
    [Required]
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid? EngagementId { get; set; }
    [Required, StringLength(50)]
    public string RequestNumber { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string RequestTypeCode { get; set; } = "Servicing";
    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Description { get; set; }
    [Required, StringLength(50)]
    public string PriorityCode { get; set; } = "Medium";
    public Guid? AssignedToUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateServiceRequestRequest
{
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid? EngagementId { get; set; }

    [Required, StringLength(50)]
    public string RequestNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string RequestTypeCode { get; set; } = "Servicing";

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required, StringLength(50)]
    public string PriorityCode { get; set; } = "Medium";

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Open";

    public Guid? AssignedToUserId { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
