namespace Ams.Application.Common.Dtos;

public sealed class ProposalDto
{
    public Guid ProposalId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
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
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? ExternalDeliveryId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public bool CanRetry { get; set; }
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
    public bool IsSelected { get; set; }
    public int SortOrder { get; set; }
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
