namespace Ams.Application.Features.Accounts;

public sealed class UpdateAccountRequest
{
    public string AccountName { get; set; } = string.Empty;
    public string AccountTypeCode { get; set; } = string.Empty;
    public string? MainEmail { get; set; }
    public string? MainPhone { get; set; }
    public string StatusCode { get; set; } = "Active";
    public string? SegmentCode { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? ParentAccountId { get; set; }
    public string? LifecycleStageCode { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
