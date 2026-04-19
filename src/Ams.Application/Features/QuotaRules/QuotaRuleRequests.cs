namespace Ams.Application.Features.QuotaRules;

public sealed class CreateQuotaRuleRequest
{
    public string? RuleCode             { get; set; }
    public string  PlanCode             { get; set; } = string.Empty;
    public string  MetricTypeCode       { get; set; } = string.Empty;
    public decimal LimitValue           { get; set; }
    public string  LimitUnit            { get; set; } = "Count";
    public string  PeriodCode           { get; set; } = "Monthly";
    public decimal WarningThresholdPct  { get; set; } = 80;
    public decimal GraceThreshold       { get; set; }
    public bool    OverageBillingEnabled { get; set; }
    public string  EnforcementMode      { get; set; } = "Hard";
    public bool    IsEnforced           { get; set; } = true;
    public string? Notes                { get; set; }
    public Guid?   CreatedByUserId      { get; set; }
}

public sealed class UpdateQuotaRuleRequest
{
    public string? RuleCode             { get; set; }
    public string  PlanCode             { get; set; } = string.Empty;
    public string  MetricTypeCode       { get; set; } = string.Empty;
    public decimal LimitValue           { get; set; }
    public string  LimitUnit            { get; set; } = "Count";
    public string  PeriodCode           { get; set; } = "Monthly";
    public decimal WarningThresholdPct  { get; set; } = 80;
    public decimal GraceThreshold       { get; set; }
    public bool    OverageBillingEnabled { get; set; }
    public string  EnforcementMode      { get; set; } = "Hard";
    public bool    IsEnforced           { get; set; }
    public bool    IsActive             { get; set; }
    public string? Notes                { get; set; }
}

public sealed class CloneQuotaRuleRequest
{
    public string? NewRuleCode { get; set; }
    public Guid?   CreatedByUserId { get; set; }
}
