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
