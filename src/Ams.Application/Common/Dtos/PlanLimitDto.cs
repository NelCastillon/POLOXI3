namespace Ams.Application.Common.Dtos;

public sealed class PlanLimitDto
{
    public Guid    PlanLimitId    { get; set; }
    public Guid    PlanId         { get; set; }
    public string  MetricTypeCode { get; set; } = string.Empty;
    public decimal LimitValue     { get; set; }
    public string  LimitUnit      { get; set; } = string.Empty;
    public string  PeriodCode     { get; set; } = string.Empty;
    public bool    IsEnforced     { get; set; }
    public string  Notes          { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
