namespace Ams.Application.Common.Dtos;

public sealed class CommissionSplitRuleDto
{
    public Guid SplitRuleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CommissionPlanId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string SplitTypeCode { get; set; } = string.Empty;
    public Guid? PayeeId { get; set; }
    public decimal SplitPct { get; set; }
    public decimal? OverrideRatePct { get; set; }
    public int Priority { get; set; }
    public DateOnly EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
