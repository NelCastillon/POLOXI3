using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Engagements;

public sealed class CreateEngagementRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string EngagementNumber { get; set; } = string.Empty;

    [Required]
    public Guid AccountId { get; set; }

    public Guid? AgreementId { get; set; }

    [Required]
    [StringLength(200)]
    public string EngagementName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EngagementTypeCode { get; set; } = "Project";

    public Guid? OwnerUserId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
