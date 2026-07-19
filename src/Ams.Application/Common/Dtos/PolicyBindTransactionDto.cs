namespace Ams.Application.Common.Dtos;

public sealed class PolicyBindTransactionDto
{
    public Guid PolicyBindTransactionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid QuoteId { get; set; }
    public string? QuoteNumber { get; set; }
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string PolicySourceCode { get; set; } = "QuoteBound";
    public string PolicySourceName { get; set; } = "Quote Bound";
    public string BindStatusCode { get; set; } = "Bound";
    public string BindStatusName { get; set; } = "Bound";
    public string? BindReason { get; set; }
    public string? Notes { get; set; }
    public decimal AnnualPremium { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime RequestedDateUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public Guid? BoundByUserId { get; set; }
    public DateTime? BoundDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
