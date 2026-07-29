namespace Ams.Application.Common.Dtos;

public sealed class QuoteComparisonDto
{
    public Guid QuoteId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string QuoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GovernanceStatusCode { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public string? CoverageForms { get; set; }
    public decimal? CommissionPercent { get; set; }
    public string? Subjectivities { get; set; }
    public string? Exclusions { get; set; }
    public string? CarrierRating { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? MinimumEarnedPremium { get; set; }
    public decimal? TaxesAndFees { get; set; }
    public decimal? BrokerFee { get; set; }
    public bool? TriaIncluded { get; set; }
    public bool IsBindable { get; set; }
    public Guid? QuoteDocumentId { get; set; }
    public string? QuoteDocumentFileName { get; set; }
    public Guid? DisclosureDocumentId { get; set; }
    public bool IsReviewed { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
    public Guid? ApprovedForPresentationByUserId { get; set; }
    public DateTime? ApprovedForPresentationDateUtc { get; set; }
    public string? PresentationReadinessNotes { get; set; }
    public bool IsProposalReady { get; set; }
    public string? ProposalReadinessReason { get; set; }
    public bool IsSelected { get; set; }
    public bool IsRecommended { get; set; }
    public int RecommendationScore { get; set; }
    public string? RecommendationReason { get; set; }
    public string? CoverageNotes { get; set; }
    public DateTime? QuoteRequestDateUtc { get; set; }
    public DateTime? QuoteReceivedDateUtc { get; set; }
    public int ResponseVersion { get; set; }
    public string? ResponseSourceCode { get; set; }
    public string? CarrierReferenceNumber { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public DateTime QuotedDateUtc { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public IReadOnlyList<SubmissionQuoteLineDto> Lines { get; set; } = [];
    public IReadOnlyList<ProposalReadinessFactorDto> ProposalReadinessFactors { get; set; } = [];
    public int LineCount => Lines.Count;
}

public sealed class ProposalReadinessFactorDto
{
    public Guid ProposalReadinessFactorId { get; set; }
    public Guid TenantId { get; set; }
    public Guid QuoteId { get; set; }
    public string FactorCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsSatisfied { get; set; }
    public int SortOrder { get; set; }
}

public sealed class SubmissionQuoteLineDto
{
    public Guid QuoteLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? SubmissionLineId { get; set; }
    public Guid? OpportunityLineId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal QuotedPremium { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public decimal? CommissionPercent { get; set; }
    public string? CoverageForms { get; set; }
    public string? Subjectivities { get; set; }
    public string? Exclusions { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? MinimumEarnedPremium { get; set; }
    public decimal? TaxesAndFees { get; set; }
    public decimal? BrokerFee { get; set; }
    public bool? TriaIncluded { get; set; }
    public bool IsBindable { get; set; }
    public string? CoverageNotes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class ProposalWorkflowDto
{
    public Guid ProposalId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GovernanceStatusCode { get; set; } = string.Empty;
    public string? DeliveryMethod { get; set; }
    public string? Recipient { get; set; }
    public string? DeliveryStatus { get; set; }
    public Guid? LastDeliveryDispatchId { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public DateTime? PresentedDateUtc { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public DateTime? DeliveryConfirmedDateUtc { get; set; }
    public Guid? CurrentReviewId { get; set; }
    public string? ClientDecision { get; set; }
    public string? DecisionNotes { get; set; }
    public DateTime? DecisionDateUtc { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentFileName { get; set; }
}
