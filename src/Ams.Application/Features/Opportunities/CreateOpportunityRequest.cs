using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Opportunities;

public sealed class CreateOpportunityRequest
{
    public Guid TenantId { get; set; }
    [StringLength(50)]
    public string OpportunityNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    [Required, StringLength(200)]
    public string OpportunityName { get; set; } = string.Empty;
    [Range(0, 999999999999)]
    public decimal EstimatedAmount { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? CloseDate { get; set; }
    public Guid? LeadId { get; set; }
    [Range(0, 100)]
    public decimal WinProbability { get; set; }
    [Required, StringLength(50)]
    public string ForecastCategoryCode { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}
