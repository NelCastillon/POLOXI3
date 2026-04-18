namespace Ams.Application.Features.PricingRules;

public sealed class CreatePricingRuleRequest
{
    public Guid TenantId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleTypeCode { get; set; } = "Discount";
    public string? ServiceCode { get; set; }
    public string? SegmentCode { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal? AdjustedUnitPrice { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public bool RequiresApproval { get; set; }
    public int Priority { get; set; } = 10;
    public Guid? CreatedByUserId { get; set; }
}
