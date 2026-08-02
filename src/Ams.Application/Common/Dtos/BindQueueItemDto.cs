namespace Ams.Application.Common.Dtos;

public sealed class BindQueueItemDto
{
    public Guid QuoteId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string SubmissionNumber { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string ProducerName { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string QuoteNumber { get; set; } = string.Empty;
    public string QuoteStatus { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public decimal? CommissionPercent { get; set; }
    public string? Subjectivities { get; set; }
    public string? Exclusions { get; set; }
    public string? CarrierRating { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? MinimumEarnedPremium { get; set; }
    public decimal? TaxesAndFees { get; set; }
    public decimal? BrokerFee { get; set; }
    public bool? TriaIncluded { get; set; }
    public Guid? QuoteDocumentId { get; set; }
    public string? CoverageNotes { get; set; }
    public bool IsSelected { get; set; }
    public bool IsRecommended { get; set; }
    public int RecommendationScore { get; set; }
    public string? RecommendationReason { get; set; }
    public DateTime QuoteExpiresDateUtc { get; set; }
    public Guid? PolicyBindTransactionId { get; set; }
    public Guid? PolicyId { get; set; }
    public string? PolicyNumber { get; set; }
    public string? BindStatusCode { get; set; }
    public string? BindStatusName { get; set; }
    public string? BindReason { get; set; }
    public string? BindNotes { get; set; }
    public DateTime? BindCreatedDateUtc { get; set; }
    public bool IsReadyToSubmit { get; set; }
    public int BlockingValidationCount { get; set; }
    public bool ApprovalRequired { get; set; }
    public bool PaymentRequired { get; set; }
    public bool PaymentVerified { get; set; }
    public DateTime? ResponseDueDateUtc { get; set; }
}
