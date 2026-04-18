namespace Ams.Application.Features.Opportunities;

public sealed class CreateOpportunityRequest
{
    public Guid TenantId { get; set; }
    public string OpportunityNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public decimal EstimatedAmount { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? CloseDate { get; set; }
    public Guid? LeadId { get; set; }
    public decimal WinProbability { get; set; }
    public string ForecastCategoryCode { get; set; } = "Pipeline";
    public Guid? CreatedByUserId { get; set; }
}
