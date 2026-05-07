using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PricingRules;

public sealed class CreatePricingRuleRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(80)]
    public string RuleCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string RuleTypeCode { get; set; } = "Discount";

    [StringLength(80)]
    public string? ServiceCode { get; set; }

    [StringLength(80)]
    public string? SegmentCode { get; set; }

    [Range(0, 999999999)]
    public decimal? MinQuantity { get; set; }

    [Range(0, 999999999)]
    public decimal? MaxQuantity { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Range(0, 999999999)]
    public decimal? AdjustedUnitPrice { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public bool RequiresApproval { get; set; }

    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 10;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdatePricingRuleRequest
{
    [Required, StringLength(80)]
    public string RuleCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string RuleTypeCode { get; set; } = "Discount";

    [StringLength(80)]
    public string? ServiceCode { get; set; }

    [StringLength(80)]
    public string? SegmentCode { get; set; }

    [Range(0, 999999999)]
    public decimal? MinQuantity { get; set; }

    [Range(0, 999999999)]
    public decimal? MaxQuantity { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Range(0, 999999999)]
    public decimal? AdjustedUnitPrice { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public bool RequiresApproval { get; set; }

    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public Guid? ModifiedByUserId { get; set; }
}
