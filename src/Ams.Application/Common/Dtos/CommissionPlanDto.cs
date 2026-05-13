namespace Ams.Application.Common.Dtos;

public sealed class CommissionPlanDto
{
    public Guid CommissionPlanId { get; set; }
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PlanTypeCode { get; set; } = "Standard";
    public decimal NewBusinessRatePct { get; set; }
    public decimal RenewalRatePct { get; set; }
    public DateOnly EffectiveStartDate { get; set; }
    public string StatusCode { get; set; } = "Draft";
    public bool AllowSplit { get; set; }
    public bool HouseAccountRules { get; set; }
    public bool BranchOverrideEligible { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
