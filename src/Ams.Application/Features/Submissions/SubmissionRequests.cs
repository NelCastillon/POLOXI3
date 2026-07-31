using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Submissions;

public sealed record CreateSubmissionRequest(
    Guid TenantId,
    Guid AccountId,
    Guid OpportunityId,
    [property: Required, StringLength(100)]
    string LineOfBusiness,
    [property: Required, StringLength(50)]
    string Priority,
    DateTime EffectiveDate,
    DateTime ExpirationDate,
    [property: Range(0, 999999999999)]
    decimal? TargetPremium,
    Guid? AssignedToUserId);

public sealed record UpdateSubmissionRequest(
    [property: Required, StringLength(100)]
    string LineOfBusiness,
    [property: Required, StringLength(50)]
    string Status,
    [property: Required, StringLength(50)]
    string Priority,
    DateTime EffectiveDate,
    DateTime ExpirationDate,
    [property: Range(0, 999999999999)]
    decimal? TargetPremium,
    Guid? AssignedToUserId);

public sealed record AssignSubmissionRequest(Guid AssignedToUserId);

public sealed record SubmissionActionResult(Guid Id, string Message);

public sealed record AddSubmissionNoteRequest(
    Guid TenantId,
    [param: Required, StringLength(1000)]
    string Notes,
    Guid? CreatedByUserId);

public sealed record CreateSubmissionFollowUpTaskRequest(
    Guid TenantId,
    [property: Required, StringLength(200)]
    string Title,
    [property: StringLength(2000)]
    string? Description,
    [property: Required, StringLength(50)]
    string PriorityCode,
    Guid? AssignedToUserId,
    DateOnly? DueDate,
    Guid? CreatedByUserId);

public sealed record UpdateSubmissionIntakeQuestionRequest(
    Guid TenantId,
    [param: StringLength(2000)]
    string? AnswerText,
    bool IsAnswered,
    Guid? AnsweredByUserId,
    [param: StringLength(50)]
    string? StatusCode = null,
    [param: StringLength(1000)]
    string? StatusReason = null,
    Guid? EvidenceDocumentId = null,
    [param: StringLength(1000)]
    string? WaiverReason = null,
    DateTime? ReviewDueDateUtc = null);

public sealed record ReplaceSubmissionReadinessEvidenceRequest(
    Guid TenantId,
    Guid[] DocumentIds,
    [property: StringLength(50)]
    string? EvidenceRoleCode = null,
    [property: StringLength(1000)]
    string? Notes = null,
    Guid? ModifiedByUserId = null);

public sealed record UpdateSubmissionMarketPackageRequest(
    Guid TenantId,
    Guid SubmissionMarketId,
    [property: Required, StringLength(50)]
    string Status,
    [property: StringLength(80)]
    string? ReasonCode,
    [property: StringLength(1000)]
    string? Notes,
    DateTime? NextActionDateUtc,
    Guid[] DocumentIds,
    Guid? ModifiedByUserId,
    [property: StringLength(200)]
    string? UnderwriterName = null,
    [property: StringLength(320)]
    string? UnderwriterEmail = null,
    [property: StringLength(50)]
    string? UnderwriterPhone = null,
    DateTime? DueDateUtc = null,
    [property: StringLength(1000)]
    string? RequestedCoverageSummary = null,
    [property: StringLength(1000)]
    string? RequestedLimits = null,
    [property: StringLength(50)]
    string? SubmissionMethodCode = null,
    Guid? FollowUpTaskId = null);

public sealed record UpdateSubmissionQuoteRequest(
    Guid TenantId,
    [property: Required, StringLength(50)]
    string Status,
    [property: Range(0, 999999999999)]
    decimal AnnualPremium,
    [property: Range(0, 999999999999)]
    decimal? Deductible,
    [property: Range(0, 999999999999)]
    decimal? Limit,
    [property: Range(0, 100)]
    decimal? CommissionPercent,
    [property: StringLength(2000)]
    string? Subjectivities,
    [property: StringLength(2000)]
    string? Exclusions,
    [property: StringLength(80)]
    string? CarrierRating,
    [property: StringLength(200)]
    string? PaymentTerms,
    [property: Range(0, 999999999999)]
    decimal? MinimumEarnedPremium,
    [property: Range(0, 999999999999)]
    decimal? TaxesAndFees,
    [property: Range(0, 999999999999)]
    decimal? BrokerFee,
    bool? TriaIncluded,
    Guid? QuoteDocumentId,
    [property: StringLength(1000)]
    string? CoverageNotes,
    DateTime ExpiresDateUtc,
    Guid? ModifiedByUserId,
    [property: StringLength(50)]
    string? ResponseSourceCode = null,
    [property: StringLength(100)]
    string? CarrierReferenceNumber = null,
    Guid? ReceivedByUserId = null,
    Guid? SubmissionMarketId = null,
    DateTime? EffectiveDate = null,
    [property: StringLength(2000)]
    string? CoverageForms = null,
    bool IsBindable = false,
    IReadOnlyList<SubmissionQuoteLineTermRequest>? Lines = null);

public sealed record RecordSubmissionQuoteResponseRequest(
    Guid TenantId,
    Guid SubmissionMarketId,
    Guid? QuoteId,
    [param: Required, StringLength(50)]
    string Status,
    [param: Range(0, 999999999999)]
    decimal AnnualPremium,
    [param: Range(0, 999999999999)]
    decimal? Deductible,
    [param: Range(0, 999999999999)]
    decimal? Limit,
    [param: Range(0, 100)]
    decimal? CommissionPercent,
    [param: StringLength(2000)]
    string? Subjectivities,
    [param: StringLength(2000)]
    string? Exclusions,
    [param: StringLength(80)]
    string? CarrierRating,
    [param: StringLength(200)]
    string? PaymentTerms,
    [param: Range(0, 999999999999)]
    decimal? MinimumEarnedPremium,
    [param: Range(0, 999999999999)]
    decimal? TaxesAndFees,
    [param: Range(0, 999999999999)]
    decimal? BrokerFee,
    bool? TriaIncluded,
    Guid? QuoteDocumentId,
    [param: StringLength(1000)]
    string? CoverageNotes,
    DateTime ExpiresDateUtc,
    [param: StringLength(50)]
    string? ResponseSourceCode,
    [param: StringLength(100)]
    string? CarrierReferenceNumber,
    Guid? ReceivedByUserId,
    DateTime? EffectiveDate = null,
    [param: StringLength(2000)]
    string? CoverageForms = null,
    bool IsBindable = false,
    IReadOnlyList<SubmissionQuoteLineTermRequest>? Lines = null);

public sealed class SubmissionQuoteLineTermRequest
{
    public SubmissionQuoteLineTermRequest(
        Guid submissionLineId,
        string lineOfBusiness,
        string status,
        decimal quotedPremium,
        decimal? deductible,
        decimal? limit,
        decimal? commissionPercent,
        string? coverageForms,
        string? subjectivities,
        string? exclusions,
        string? paymentTerms,
        decimal? minimumEarnedPremium,
        decimal? taxesAndFees,
        decimal? brokerFee,
        bool? triaIncluded,
        bool isBindable,
        string? coverageNotes,
        int sortOrder = 0)
    {
        SubmissionLineId = submissionLineId;
        LineOfBusiness = lineOfBusiness;
        Status = status;
        QuotedPremium = quotedPremium;
        Deductible = deductible;
        Limit = limit;
        CommissionPercent = commissionPercent;
        CoverageForms = coverageForms;
        Subjectivities = subjectivities;
        Exclusions = exclusions;
        PaymentTerms = paymentTerms;
        MinimumEarnedPremium = minimumEarnedPremium;
        TaxesAndFees = taxesAndFees;
        BrokerFee = brokerFee;
        TriaIncluded = triaIncluded;
        IsBindable = isBindable;
        CoverageNotes = coverageNotes;
        SortOrder = sortOrder;
    }

    public Guid SubmissionLineId { get; init; }
    [Required, StringLength(100)] public string LineOfBusiness { get; init; }
    [Required, StringLength(50)] public string Status { get; init; }
    [Range(0, 999999999999)] public decimal QuotedPremium { get; init; }
    [Range(0, 999999999999)] public decimal? Deductible { get; init; }
    [Range(0, 999999999999)] public decimal? Limit { get; init; }
    [Range(0, 100)] public decimal? CommissionPercent { get; init; }
    [StringLength(2000)] public string? CoverageForms { get; init; }
    [StringLength(2000)] public string? Subjectivities { get; init; }
    [StringLength(2000)] public string? Exclusions { get; init; }
    [StringLength(200)] public string? PaymentTerms { get; init; }
    [Range(0, 999999999999)] public decimal? MinimumEarnedPremium { get; init; }
    [Range(0, 999999999999)] public decimal? TaxesAndFees { get; init; }
    [Range(0, 999999999999)] public decimal? BrokerFee { get; init; }
    public bool? TriaIncluded { get; init; }
    public bool IsBindable { get; init; }
    [StringLength(1000)] public string? CoverageNotes { get; init; }
    public int SortOrder { get; init; }
}

public sealed record RecordCarrierInboundResponseRequest(
    Guid TenantId,
    Guid? SubmissionMarketId,
    Guid? CarrierTransmissionId,
    Guid? CarrierId,
    [property: Required, StringLength(50)]
    string SourceChannelCode,
    [property: Required, StringLength(50)]
    string ResponseTypeCode,
    [property: Required, StringLength(50)]
    string StatusCode,
    [property: StringLength(120)]
    string? CarrierReferenceNumber,
    [property: Required]
    string PayloadJson,
    DateTime? ReceivedDateUtc = null,
    Guid? CreatedByUserId = null);

public sealed record SelectSubmissionQuoteRequest(
    Guid TenantId,
    Guid QuoteId,
    bool IsRecommended,
    [param: Required, StringLength(1000)]
    string Reason,
    Guid? SelectedByUserId);

public sealed record ProposalDeliveryRequest(
    Guid TenantId,
    [Required, StringLength(50)]
    string DeliveryMethod,
    [Required, StringLength(320)]
    string Recipient,
    Guid? SentByUserId);

public sealed record RetryProposalDeliveryRequest(
    Guid TenantId,
    Guid? RequestedByUserId);

public sealed record UpdateProposalDeliveryRecipientRequest(
    Guid TenantId,
    [Required, EmailAddress, StringLength(320)]
    string Recipient,
    [StringLength(1000)]
    string? ChangeReason,
    Guid? ModifiedByUserId);

public sealed record ResendProposalDeliveryRequest(
    Guid TenantId,
    [StringLength(320)]
    string? Recipient,
    [StringLength(1000)]
    string? Reason,
    Guid? RequestedByUserId);

public sealed record DeleteProposalDeliveryRequest(
    Guid TenantId,
    [Required, StringLength(1000)]
    string Reason,
    Guid? DeletedByUserId);

public sealed record UpdateProposalDeliveryProviderRequest(
    Guid TenantId,
    [StringLength(1000)]
    string? EndpointUri,
    [EmailAddress, StringLength(320)]
    string? SenderAddress,
    [StringLength(500)]
    string? SecretReference,
    string? ConfigurationJson,
    bool IsConfigured,
    bool IsActive,
    [Range(1, 25)]
    int MaxAttempts,
    [Range(10, 86400)]
    int RetryDelaySeconds,
    Guid? ModifiedByUserId);

public sealed record ProposalPresentationRequest(
    Guid TenantId,
    [StringLength(1000)]
    string? PresentationNotes,
    Guid? PresentedByUserId);

public sealed record SubmitSubmissionToMarketRequest(
    Guid TenantId,
    Guid? CarrierId,
    [StringLength(500)]
    string? Notes);

public sealed record UpsertSubmissionReadinessRequirementRequest(
    Guid TenantId,
    [property: Required, StringLength(100)]
    string LineOfBusiness,
    Guid? CarrierId,
    [property: StringLength(20)]
    string? StateCode,
    [property: StringLength(50)]
    string? ChannelCode,
    [property: Required, StringLength(50)]
    string ScopeCode,
    [property: Required, StringLength(100)]
    string RequirementCode,
    [property: Required, StringLength(50)]
    string RequirementTypeCode,
    [property: Required, StringLength(200)]
    string DisplayName,
    [property: StringLength(1000)]
    string? Description,
    bool IsRequired,
    bool BlocksSubmit,
    bool AllowsWaiver,
    bool RequiresEvidence,
    [property: StringLength(500)]
    string? EvidencePrompt,
    [property: StringLength(100)]
    string? ApprovalRoleCode,
    [property: StringLength(50)]
    string? ActionCode,
    [property: StringLength(150)]
    string? ActionLabel,
    [property: Range(0, 100)]
    int ScoreWeight,
    [property: Range(0, 10000)]
    int SortOrder,
    bool IsActive,
    Guid? ModifiedByUserId);

public sealed record RequestSubmissionQuoteRequest(
    Guid TenantId,
    Guid? CarrierId,
    [Range(0, 999999999999)]
    decimal? AnnualPremium,
    [Range(0, 999999999999)]
    decimal? Deductible,
    [Range(0, 999999999999)]
    decimal? Limit,
    [StringLength(1000)]
    string? CoverageNotes,
    Guid? RequestedByUserId = null,
    [StringLength(100)]
    string? CarrierReferenceNumber = null,
    Guid? SubmissionMarketId = null,
    [StringLength(50)]
    string? QuoteRequestScopeCode = null,
    Guid[]? SubmissionLineIds = null,
    [StringLength(50)]
    string? QuoteRequestActionCode = null,
    [StringLength(80)]
    string? QuoteRequestReasonCode = null,
    [StringLength(50)]
    string? QuoteRequestMethodCode = null);

public sealed record CopySubmissionRequest(
    Guid TenantId,
    DateTime? EffectiveDate,
    [property: StringLength(100)]
    string? LineOfBusiness,
    [property: StringLength(50)]
    string? Priority);

public sealed record DeclineSubmissionRequest(
    Guid TenantId,
    [property: Required, StringLength(500)]
    string Reason);

public sealed record CreatePolicyFromSubmissionRequest(
    Guid TenantId,
    Guid? QuoteId,
    Guid? CarrierId,
    [property: Range(0, 999999999999)]
    decimal? AnnualPremium,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate,
    [property: StringLength(80)]
    string? PolicyNumber,
    [property: Required, StringLength(50)]
    string PolicySourceCode = "QuoteBound",
    [property: StringLength(500)]
    string? PolicySourceReason = null,
    [property: StringLength(1000)]
    string? PolicySourceNotes = null,
    Guid? ProposalId = null,
    Guid? CustomerAuthorizationId = null,
    [property: StringLength(50)]
    string? CustomerAuthorizationMethodCode = null,
    [property: StringLength(200)]
    string? CustomerAuthorizationReference = null,
    [property: StringLength(2000)]
    string? CustomerAuthorizationNotes = null,
    [property: StringLength(200)]
    string? CustomerAuthorizedByName = null,
    DateTime? CustomerAuthorizedDateUtc = null,
    Guid? CustomerAuthorizationDocumentId = null);

public sealed record AddSubmissionMarketRequest(
    Guid SubmissionId,
    Guid CarrierId);

public sealed record UpdateSubmissionMarketStatusRequest(string Status, string? DeclineReason);

public sealed record GenerateProposalRequest(
    Guid SubmissionId,
    Guid TenantId,
    string Title,
    Guid[] QuoteIds,
    string? CustomIntroduction,
    Guid? GeneratedByUserId = null);

public sealed record SubmitProposalReviewRequest(
    Guid TenantId,
    Guid AssignedReviewerUserId,
    [StringLength(2000)] string? ReviewNotes,
    DateTime? DueDateUtc,
    Guid? RequestedByUserId = null);

public sealed record DecideProposalReviewRequest(
    Guid TenantId,
    [Required] string DecisionCode,
    [Required, StringLength(2000)] string DecisionNotes,
    Guid? DecidedByUserId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DecisionCode is not ("Approved" or "ChangesRequired" or "Rejected"))
            yield return new ValidationResult("Decision must be Approved, ChangesRequired, or Rejected.", [nameof(DecisionCode)]);
    }
}

public sealed record UpsertProposalRecipientRequest(
    Guid TenantId,
    Guid? ProposalRecipientId,
    Guid? ContactId,
    [Required, StringLength(50)] string RecipientTypeCode,
    [Required, StringLength(200)] string RecipientName,
    [Required, EmailAddress, StringLength(320)] string RecipientEmail,
    [Range(1, 100)] int SigningOrder,
    bool IsPrimary,
    bool IsSigner,
    Guid? ModifiedByUserId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RecipientTypeCode is not ("Client" or "Cc" or "Signer" or "Agency"))
            yield return new ValidationResult("Recipient type is invalid.", [nameof(RecipientTypeCode)]);
        if (RecipientTypeCode == "Signer" && !IsSigner)
            yield return new ValidationResult("Signer recipients must be marked as signers.", [nameof(IsSigner)]);
    }
}

public sealed record ProposalProviderCallbackRequest(
    Guid TenantId,
    [Required, StringLength(100)] string ProviderCode,
    [Required, StringLength(500)] string ProviderEventId,
    [StringLength(500)] string? ExternalEnvelopeId,
    [Required, StringLength(100)] string EventTypeCode,
    [Required, StringLength(50)] string StatusCode,
    [Required] string PayloadJson,
    [Required, StringLength(2000)] string SignatureHeader,
    Guid? SignedDocumentId = null,
    Guid? CertificateDocumentId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ProviderCode))
            yield return new ValidationResult("Provider code is required.", [nameof(ProviderCode)]);
        if (string.IsNullOrWhiteSpace(ProviderEventId))
            yield return new ValidationResult("Provider event ID is required.", [nameof(ProviderEventId)]);
        if (string.IsNullOrWhiteSpace(EventTypeCode))
            yield return new ValidationResult("Event type is required.", [nameof(EventTypeCode)]);
        if (string.IsNullOrWhiteSpace(StatusCode))
            yield return new ValidationResult("Status is required.", [nameof(StatusCode)]);
        if (string.IsNullOrWhiteSpace(PayloadJson))
            yield return new ValidationResult("Payload is required.", [nameof(PayloadJson)]);
        if (string.IsNullOrWhiteSpace(SignatureHeader))
            yield return new ValidationResult("Signature header is required.", [nameof(SignatureHeader)]);
    }
}

public sealed record UpsertProposalSlaPolicyRequest(
    Guid TenantId,
    [Required, StringLength(100)] string EventCode,
    [Range(1, 525600)] int DueAfterMinutes,
    [Range(1, 525600)] int? EscalateAfterMinutes,
    [Required, StringLength(50)] string PriorityCode,
    [StringLength(100)] string? AssignedRoleCode,
    bool IsActive,
    Guid? ModifiedByUserId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EscalateAfterMinutes.HasValue && EscalateAfterMinutes < DueAfterMinutes)
            yield return new ValidationResult("Escalation cannot occur before the SLA due time.", [nameof(EscalateAfterMinutes)]);
    }
}

public sealed record UpsertSubmissionIntakeTemplateRequest(
    Guid TenantId,
    [property: Required, StringLength(100)]
    string LineOfBusiness,
    [property: Required, StringLength(100)]
    string QuestionCode,
    [property: Required, StringLength(500)]
    string QuestionText,
    [property: StringLength(1000)]
    string? HelpText,
    bool IsRequired,
    int SortOrder,
    bool IsActive,
    Guid? ModifiedByUserId);

public sealed record UpsertSubmissionDocumentRequirementRequest(
    Guid TenantId,
    [property: Required, StringLength(100)]
    string LineOfBusiness,
    [property: Required, StringLength(100)]
    string CategoryCode,
    [property: Required, StringLength(200)]
    string DisplayName,
    bool IsRequired,
    int SortOrder,
    bool IsActive,
    Guid? ModifiedByUserId);

public sealed record AppetiteSearchRequest(
    Guid TenantId,
    string LineOfBusiness,
    string? State,
    decimal? TotalInsuredValue,
    string? ConstructionType,
    string? OccupancyType,
    int? YearBuilt,
    string[] AdditionalCriteria);

public sealed record BindPolicyRequest(
    Guid? SubmissionId,
    Guid? QuoteId,
    Guid TenantId,
    Guid AccountId,
    Guid CarrierId,
    decimal AnnualPremium,
    DateTime EffectiveDate,
    DateTime ExpirationDate,
    string? PolicyNumber = null,
    string PolicySourceCode = "QuoteBound",
    string? PolicySourceReason = null,
    string? PolicySourceNotes = null,
    Guid? RequestedByUserId = null,
    Guid? ApprovedByUserId = null,
    Guid? BoundByUserId = null,
    string BindStatusCode = "Pending",
    Guid? ProposalId = null,
    Guid? CustomerAuthorizationId = null,
    string? CustomerAuthorizationMethodCode = null,
    string? CustomerAuthorizationReference = null,
    string? CustomerAuthorizationNotes = null,
    string? CustomerAuthorizedByName = null,
    DateTime? CustomerAuthorizedDateUtc = null,
    Guid? CustomerAuthorizationDocumentId = null,
    TimeSpan? RequestedEffectiveTime = null,
    string? ConfirmationSourceCode = null,
    string? CarrierReferenceNumber = null,
    string? BinderNumber = null,
    decimal? FinalPremium = null,
    decimal? DownPaymentAmount = null,
    string? SubjectivitiesOutstanding = null,
    string? ConfirmationNotes = null,
    Guid? ConfirmationDocumentId = null,
    string? ConfirmationReceivedFrom = null,
    string? ConfirmationMessageId = null,
    string? UnderwriterName = null,
    string? UnderwriterCompany = null,
    bool FollowUpWrittenConfirmationRequired = false,
    string? IntegrationCorrelationId = null,
    string? ExternalTransactionId = null,
    bool ConfirmedManually = false,
    bool ConfirmationCertified = false);

public sealed class UpsertPolicyRegisterRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(80)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required]
    public Guid AccountId { get; set; }

    public Guid? SubmissionId { get; set; }

    public Guid? QuoteId { get; set; }

    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string AccountType { get; set; } = "Commercial";

    [Required, StringLength(160)]
    public string CarrierName { get; set; } = string.Empty;

    public Guid? CarrierId { get; set; }

    [Required, StringLength(50)]
    public string PolicySourceCode { get; set; } = "ManualEntry";

    [StringLength(500)]
    public string? PolicySourceReason { get; set; }

    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Status { get; set; } = "Active";

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    public DateTime ExpirationDate { get; set; }

    [Range(0, 999999999999)]
    public decimal WrittenPremium { get; set; }

    [Range(0, 999999999999)]
    public decimal AnnualPremium { get; set; }

    [StringLength(120)]
    public string? ProducerName { get; set; }

    [StringLength(120)]
    public string? CsrName { get; set; }

    [StringLength(80)]
    public string? Branch { get; set; }

    [StringLength(80)]
    public string? RenewalStage { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class PolicyRegisterActionRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(60)]
    public string Action { get; set; } = string.Empty;

    public DateTime? EffectiveDate { get; set; }

    [Range(-10000000, 10000000)]
    public decimal? Premium { get; set; }

    [StringLength(200)]
    public string? DocumentTitle { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
