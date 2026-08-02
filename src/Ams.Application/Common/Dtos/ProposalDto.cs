namespace Ams.Application.Common.Dtos;

public sealed class ProposalDto
{
    public Guid ProposalId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GovernanceStatusCode { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? PdfUrl { get; set; }
    public string? HtmlContent { get; set; }
    public string? CustomIntroduction { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? Recipient { get; set; }
    public string? DeliveryStatus { get; set; }
    public Guid? LastDeliveryDispatchId { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public DateTime? PresentedDateUtc { get; set; }
    public string? ClientDecision { get; set; }
    public string? DecisionNotes { get; set; }
    public DateTime? DecisionDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? GeneratedDateUtc { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public int? ApprovalVersionNumber { get; set; }
    public string? ApprovedSnapshotHash { get; set; }
    public DateTime? ReadyToDeliverDateUtc { get; set; }
    public DateTime? DeliveryConfirmedDateUtc { get; set; }
    public ProposalReviewDto? CurrentReview { get; set; }
    public IReadOnlyList<ProposalRecipientDto> Recipients { get; set; } = [];
    public IReadOnlyList<ProposalESignEnvelopeDto> ESignEnvelopes { get; set; } = [];
    public IReadOnlyList<ProposalQuoteDto> Quotes { get; set; } = [];
    public IReadOnlyList<ProposalLifecycleEventDto> Events { get; set; } = [];
    public IReadOnlyList<ProposalDeliveryDispatchDto> Deliveries { get; set; } = [];
}

public sealed class ProposalDeliveryProviderDto
{
    public Guid ProposalDeliveryProviderId { get; set; }
    public Guid TenantId { get; set; }
    public string DeliveryMethodCode { get; set; } = string.Empty;
    public string ProviderCode { get; set; } = string.Empty;
    public string HandlerCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? EndpointUri { get; set; }
    public string? SenderAddress { get; set; }
    public string? SecretReference { get; set; }
    public string? ConfigurationJson { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsActive { get; set; }
    public int MaxAttempts { get; set; }
    public int RetryDelaySeconds { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class ProposalDeliveryDispatchDto
{
    public Guid ProposalDeliveryDispatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid ProposalId { get; set; }
    public string DeliveryMethodCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int ProposalVersionNumber { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public DateTime? FirstViewedDateUtc { get; set; }
    public DateTime? LastViewedDateUtc { get; set; }
    public DateTime? DownloadedDateUtc { get; set; }
    public DateTime? SignedDateUtc { get; set; }
    public DateTime? DeclinedDateUtc { get; set; }
    public DateTime? ExpiredDateUtc { get; set; }
    public DateTime? BouncedDateUtc { get; set; }
    public DateTime? CancelledDateUtc { get; set; }
    public string? ExternalDeliveryId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public bool CanRetry { get; set; }
}

public sealed class ProposalDeliveryMonitorDto
{
    public Guid ProposalDeliveryDispatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid ProposalId { get; set; }
    public string DeliveryMethodCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int ProposalVersionNumber { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public DateTime? FirstViewedDateUtc { get; set; }
    public DateTime? LastViewedDateUtc { get; set; }
    public DateTime? DownloadedDateUtc { get; set; }
    public DateTime? SignedDateUtc { get; set; }
    public DateTime? DeclinedDateUtc { get; set; }
    public DateTime? ExpiredDateUtc { get; set; }
    public DateTime? BouncedDateUtc { get; set; }
    public DateTime? CancelledDateUtc { get; set; }
    public string? ExternalDeliveryId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public bool CanRetry { get; set; }
    public string ProposalTitle { get; set; } = string.Empty;
    public string SubmissionNumber { get; set; } = string.Empty;
    public string SubmissionStatus { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string? OpportunityName { get; set; }
    public string? AssignedProducerName { get; set; }
    public string? DeliveryHandlerCode { get; set; }
    public string? SenderAddress { get; set; }
    public bool ProviderIsConfigured { get; set; }
    public bool ProviderIsActive { get; set; }
}

public sealed class ProposalReviewDto
{
    public Guid ProposalReviewId { get; set; }
    public Guid ProposalId { get; set; }
    public int ProposalVersionNumber { get; set; }
    public int ReviewRound { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid AssignedReviewerUserId { get; set; }
    public string AssignedReviewerName { get; set; } = string.Empty;
    public DateTime RequestedDateUtc { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? DecisionNotes { get; set; }
}

public sealed class ProposalRecipientDto
{
    public Guid ProposalRecipientId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid? ContactId { get; set; }
    public string RecipientTypeCode { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public int SigningOrder { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsSigner { get; set; }
}

public sealed class ProposalESignEnvelopeDto
{
    public Guid ProposalESignEnvelopeId { get; set; }
    public Guid ProposalId { get; set; }
    public int ProposalVersionNumber { get; set; }
    public Guid ProposalDeliveryDispatchId { get; set; }
    public Guid? ESignRequestId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string ExternalEnvelopeId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? SentDateUtc { get; set; }
    public DateTime? DeliveredDateUtc { get; set; }
    public DateTime? FirstViewedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public Guid? SignedDocumentId { get; set; }
    public Guid? CertificateDocumentId { get; set; }
}

public sealed class ProposalSlaPolicyDto
{
    public Guid ProposalSlaPolicyId { get; set; }
    public Guid TenantId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public int DueAfterMinutes { get; set; }
    public int? EscalateAfterMinutes { get; set; }
    public string PriorityCode { get; set; } = string.Empty;
    public string? AssignedRoleCode { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ProposalWorkflowLaunchDto
{
    public Guid OpportunityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SubmissionId { get; set; }
    public bool HasSubmission { get; set; }
    public bool HasProposalReadyQuotes { get; set; }
    public int ProposalReadyQuoteCount { get; set; }
    public string NextActionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ProposalBindContinuationDto
{
    public Guid ProposalId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public bool CanRequestBind { get; set; }
    public Guid? SelectedQuoteId { get; set; }
    public Guid? CustomerAuthorizationId { get; set; }
    public string? BlockingReason { get; set; }
}

public sealed class ProposalQuoteDto
{
    public Guid QuoteId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public string? CoverageNotes { get; set; }
    public decimal? TaxesAndFees { get; set; }
    public decimal? BrokerFee { get; set; }
    public decimal? MinimumEarnedPremium { get; set; }
    public string? PaymentTerms { get; set; }
    public bool? TriaIncluded { get; set; }
    public bool IsBindable { get; set; }
    public string? CarrierRating { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public bool IsSelected { get; set; }
    public int SortOrder { get; set; }
    public IReadOnlyList<ProposalQuoteLineDto> Lines { get; set; } = [];
    public decimal LinePremiumTotal => Lines.Count == 0 ? AnnualPremium : Lines.Sum(x => x.QuotedPremium);
    public decimal PackageCostTotal => LinePremiumTotal + Lines.Sum(x => (x.TaxesAndFees ?? 0) + (x.BrokerFee ?? 0));
}

public sealed class ProposalQuoteLineDto
{
    public Guid QuoteLineId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid? SubmissionLineId { get; set; }
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
    public decimal TotalCost => QuotedPremium + (TaxesAndFees ?? 0) + (BrokerFee ?? 0);
}

public sealed class ProposalLifecycleEventDto
{
    public Guid ProposalLifecycleEventId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string? EventDetail { get; set; }
    public DateTime EventDateUtc { get; set; }
}

public sealed class ProposalWorkflowOptionDto
{
    public Guid ProposalWorkflowOptionId { get; set; }
    public string OptionGroupCode { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsProviderConfigured { get; set; }
    public string? ProviderName { get; set; }
}
