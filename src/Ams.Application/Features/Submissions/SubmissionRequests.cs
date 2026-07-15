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
    Guid? AnsweredByUserId);

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
    Guid? ModifiedByUserId);

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
    Guid? ModifiedByUserId);

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
    [property: StringLength(500)]
    string? Notes);

public sealed record RequestSubmissionQuoteRequest(
    Guid TenantId,
    Guid? CarrierId,
    [property: Range(0, 999999999999)]
    decimal? AnnualPremium,
    [property: Range(0, 999999999999)]
    decimal? Deductible,
    [property: Range(0, 999999999999)]
    decimal? Limit,
    [property: StringLength(1000)]
    string? CoverageNotes);

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
    DateTime? ExpirationDate);

public sealed record AddSubmissionMarketRequest(
    Guid SubmissionId,
    Guid CarrierId);

public sealed record UpdateSubmissionMarketStatusRequest(string Status, string? DeclineReason);

public sealed record GenerateProposalRequest(
    Guid SubmissionId,
    Guid TenantId,
    string Title,
    Guid[] QuoteIds,
    string? CustomIntroduction);

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
    Guid SubmissionId,
    Guid QuoteId,
    Guid TenantId,
    Guid AccountId,
    Guid CarrierId,
    decimal AnnualPremium,
    DateTime EffectiveDate,
    DateTime ExpirationDate);

public sealed class UpsertPolicyRegisterRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(80)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required]
    public Guid AccountId { get; set; }

    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string AccountType { get; set; } = "Commercial";

    [Required, StringLength(160)]
    public string CarrierName { get; set; } = string.Empty;

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
