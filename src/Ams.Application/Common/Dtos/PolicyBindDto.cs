namespace Ams.Application.Common.Dtos;

public sealed class PolicyBindDto
{
    public Guid PolicyId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid CarrierId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string IssueStatus { get; set; } = "PendingIssue";
    public string CoverageStatus { get; set; } = "Bound";
    public decimal AnnualPremium { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime BoundDateUtc { get; set; }
    public DateTime? IssuedDateUtc { get; set; }
}
