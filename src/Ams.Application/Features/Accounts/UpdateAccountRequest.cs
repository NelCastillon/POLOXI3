using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Accounts;

public sealed class UpdateAccountRequest
{
    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string AccountTypeCode { get; set; } = string.Empty;
    [AmsEmailAddress, StringLength(200)]
    public string? MainEmail { get; set; }
    [AmsPhone, StringLength(50)]
    public string? MainPhone { get; set; }
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Active";
    [StringLength(50)]
    public string? SegmentCode { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? ParentAccountId { get; set; }
    [StringLength(50)]
    public string? LifecycleStageCode { get; set; }
    [StringLength(100)]
    public string? Industry { get; set; }
    [Url, StringLength(200)]
    public string? Website { get; set; }
    [Range(0, 999999999999)]
    public decimal? AnnualRevenue { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
