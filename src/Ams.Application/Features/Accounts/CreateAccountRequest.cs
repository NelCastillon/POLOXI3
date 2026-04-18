namespace Ams.Application.Features.Accounts;

public sealed class CreateAccountRequest
{
    public Guid TenantId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
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
    public Guid? CreatedByUserId { get; set; }
}
