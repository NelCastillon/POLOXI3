namespace Ams.Application.Features.Plans;

public sealed class AddPlanFeatureRequest
{
    public Guid   PlanId      { get; set; }
    public string FeatureCode { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public bool   IsIncluded  { get; set; } = true;
    public string Notes       { get; set; } = string.Empty;
}

public sealed class UpdatePlanLimitRequest
{
    public Guid    PlanLimitId    { get; set; }
    public decimal LimitValue     { get; set; }
    public bool    IsEnforced     { get; set; } = true;
    public string  Notes          { get; set; } = string.Empty;
}

public sealed class AddPlanLimitRequest
{
    public Guid    PlanId         { get; set; }
    public string  MetricTypeCode { get; set; } = string.Empty;
    public decimal LimitValue     { get; set; }
    public string  LimitUnit      { get; set; } = "Count";
    public string  PeriodCode     { get; set; } = "Monthly";
    public bool    IsEnforced     { get; set; } = true;
    public string  Notes          { get; set; } = string.Empty;
}

public sealed class AddPlanAddOnRequest
{
    public Guid    PlanId           { get; set; }
    public string  AddOnCode        { get; set; } = string.Empty;
    public string  AddOnName        { get; set; } = string.Empty;
    public decimal Price             { get; set; }
    public string  BillingFrequency { get; set; } = "Monthly";
    public string  Description      { get; set; } = string.Empty;
}
