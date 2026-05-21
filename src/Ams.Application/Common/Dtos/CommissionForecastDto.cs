namespace Ams.Application.Common.Dtos;

public sealed class CommissionForecastDto
{
    public Guid ForecastId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CommissionPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public string ForecastNumber { get; set; } = string.Empty;
    public string ForecastName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal PipelinePremium { get; set; }
    public decimal WeightedPremium { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal ForecastCommission { get; set; }
    public decimal ConfidencePct { get; set; }
    public decimal ActualCommission { get; set; }
    public decimal VarianceAmount { get; set; }
    public string ScenarioCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
