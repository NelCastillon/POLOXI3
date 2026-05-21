namespace Ams.Application.Common.Dtos;

public sealed class CommissionPlannerScenarioDto
{
    public Guid ScenarioId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CommissionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public string ScenarioNumber { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public string ScenarioTypeCode { get; set; } = string.Empty;
    public decimal NewBusinessPremium { get; set; }
    public decimal RenewalPremium { get; set; }
    public int PolicyCount { get; set; }
    public decimal NewBusinessRatePct { get; set; }
    public decimal RenewalRatePct { get; set; }
    public decimal OverrideRatePct { get; set; }
    public string SplitTypeCode { get; set; } = string.Empty;
    public decimal PrimarySplitPct { get; set; }
    public decimal SecondarySplitPct { get; set; }
    public bool BranchOverride { get; set; }
    public bool HouseAccount { get; set; }
    public bool SharedClawbacks { get; set; }
    public decimal CancellationRatePct { get; set; }
    public decimal NsfRatePct { get; set; }
    public decimal NewBusinessCommission { get; set; }
    public decimal RenewalCommission { get; set; }
    public decimal OverrideCommission { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal ProjectedClawbacks { get; set; }
    public decimal NetPayout { get; set; }
    public decimal PrimaryNetPayout { get; set; }
    public decimal SecondaryNetPayout { get; set; }
    public decimal BranchNetPayout { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
