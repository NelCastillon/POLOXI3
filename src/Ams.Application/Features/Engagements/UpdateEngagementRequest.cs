using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Engagements;

public sealed class UpdateEngagementRequest
{
    [Required]
    public Guid AccountId { get; set; }

    public Guid? AgreementId { get; set; }

    [Required]
    [StringLength(50)]
    public string EngagementNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string EngagementName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string EngagementTypeCode { get; set; } = "Project";

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Active";

    public Guid? OwnerUserId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
