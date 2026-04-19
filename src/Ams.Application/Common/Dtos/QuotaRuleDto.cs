namespace Ams.Application.Common.Dtos;

public sealed class QuotaRuleDto
{
    public Guid    QuotaRuleId          { get; set; }
    public string? RuleCode             { get; set; }
    public string  PlanCode             { get; set; } = string.Empty;
    public string  MetricTypeCode       { get; set; } = string.Empty;
    public decimal LimitValue           { get; set; }
    public string  LimitUnit            { get; set; } = string.Empty;
    public string  PeriodCode           { get; set; } = string.Empty;
    public decimal WarningThresholdPct  { get; set; }
    public decimal GraceThreshold       { get; set; }
    public bool    OverageBillingEnabled { get; set; }
    public string  EnforcementMode      { get; set; } = "Hard";
    public bool    IsEnforced           { get; set; }
    public bool    IsActive             { get; set; }
    public string? Notes                { get; set; }
    public DateTime  CreatedDateUtc     { get; set; }
    public DateTime? ModifiedDateUtc    { get; set; }
}
