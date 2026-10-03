namespace Legal.Application.Features.ProviderPortal;

// ─────────────────────────────────────────────────────────────────────────────
//  Provider portal contracts — attorney-approved sharing policy, firm→provider
//  requests, and per matter+user review state. All DB-backed (POLOXI schema).
//  No fabricated data: the provider portal projects ONLY what these records say.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Attorney-approved sharing policy for a single (matter, provider).</summary>
public sealed record ProviderSharingPolicyDto(
    Guid ProviderSharingPolicyId,
    Guid MatterId,
    string ProviderKey,
    string? ProviderDisplayName,
    bool PortalEnabled,
    bool ShareMatterStatus,
    bool ShareCurrentStage,
    bool SharePatientTreatment,
    bool ShareOwnRecords,
    bool ShareOwnBills,
    bool ShareFirmRequests,
    bool ShareOtherProviders,
    bool ShareSettlementInfo);

/// <summary>Upsert payload for a provider sharing policy (matter + provider keyed).</summary>
public sealed record SaveProviderSharingPolicyRequest(
    Guid MatterId,
    string ProviderKey,
    string? ProviderDisplayName,
    bool PortalEnabled,
    bool ShareMatterStatus,
    bool ShareCurrentStage,
    bool SharePatientTreatment,
    bool ShareOwnRecords,
    bool ShareOwnBills,
    bool ShareFirmRequests,
    bool ShareOtherProviders,
    bool ShareSettlementInfo);

/// <summary>A single firm→provider request ("What the firm needs from you").</summary>
public sealed record ProviderRequestDto(
    Guid ProviderRequestId,
    Guid MatterId,
    string ProviderKey,
    string Title,
    string? Detail,
    string RequestKind,          // DOCUMENT / RECORD / BILL / INFO
    string StatusCode,           // OPEN / FULFILLED / CANCELLED
    DateTimeOffset RequestedDateUtc,
    DateTimeOffset? FulfilledDateUtc);

/// <summary>Create payload for a firm→provider request.</summary>
public sealed record CreateProviderRequestRequest(
    Guid MatterId,
    string ProviderKey,
    string Title,
    string? Detail,
    string RequestKind);

/// <summary>Status transition payload for an existing provider request.</summary>
public sealed record UpdateProviderRequestStatusRequest(
    Guid ProviderRequestId,
    string StatusCode);

/// <summary>
/// Review boundary for a (matter, user): the moment the matter was last opened
/// before this build, so "Since Your Last Review" can diff against it.
/// </summary>
public sealed record MatterReviewStateDto(
    Guid MatterId,
    Guid UserId,
    DateTimeOffset LastOpenedUtc,
    DateTimeOffset? PreviousOpenedUtc);

/// <summary>
/// A single material change detected after the review boundary, derived from
/// real document/evidence timestamps. Never fabricated.
/// </summary>
public sealed record MatterReviewDeltaDto(
    string Kind,                 // NEW DOCUMENT / NEW EVIDENCE / CASE ACTIVITY
    string Title,
    string? Detail,
    DateTimeOffset OccurredUtc,
    bool IsMaterial);
