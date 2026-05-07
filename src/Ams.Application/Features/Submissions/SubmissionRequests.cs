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
