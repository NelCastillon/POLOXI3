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
    [property: Required, StringLength(1000)]
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
    [property: StringLength(2000)]
    string? AnswerText,
    bool IsAnswered,
    Guid? AnsweredByUserId,
    [property: StringLength(50)]
    string? StatusCode = null,
    [property: StringLength(1000)]
    string? StatusReason = null,
    Guid? EvidenceDocumentId = null,
    [property: StringLength(1000)]
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
    bool IsBindable = false);

public sealed record RecordSubmissionQuoteResponseRequest(
    Guid TenantId,
    Guid SubmissionMarketId,
    Guid? QuoteId,
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
    [property: StringLength(50)]
    string? ResponseSourceCode,
    [property: StringLength(100)]
    string? CarrierReferenceNumber,
    Guid? ReceivedByUserId,
    DateTime? EffectiveDate = null,
    [property: StringLength(2000)]
    string? CoverageForms = null,
    bool IsBindable = false);

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
    [property: Required, StringLength(1000)]
    string Reason,
    Guid? SelectedByUserId);

public sealed record ProposalDeliveryRequest(
    Guid TenantId,
    [property: Required, StringLength(50)]
    string DeliveryMethod,
    [property: Required, StringLength(320)]
    string Recipient,
    Guid? SentByUserId);

public sealed record ProposalDecisionRequest(
    Guid TenantId,
    [property: Required, StringLength(50)]
    string Decision,
    [property: StringLength(1000)]
    string? DecisionNotes,
    Guid? DecidedByUserId);

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
