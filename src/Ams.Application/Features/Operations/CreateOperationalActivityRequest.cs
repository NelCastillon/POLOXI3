using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateOperationalActivityRequest
{
    public Guid TenantId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? EngagementId { get; set; }
    public Guid? AgreementId { get; set; }
    public DateOnly ActivityDate { get; set; }
    [Required, StringLength(50)]
    public string ActivityTypeCode { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
