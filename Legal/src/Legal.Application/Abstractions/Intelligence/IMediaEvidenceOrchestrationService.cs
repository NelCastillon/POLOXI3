using Legal.Application.Features.Intelligence.Decision.Lpi;
using Legal.Application.Features.Intelligence.Decision.Media;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Media & Machine Evidence orchestration (Phase 3, Slice 1) — the SENDING half.
//
// Runs the capability-aware processor for an asset version (PHOTO → image analysis, AUDIO → timestamped
// transcription), records a MediaProcessingRun (idempotent), stores derivatives, then runs the DB-backed
// MEDIA_EXTRACTION_V1 prompt over the media-derived content to produce ATOMIC proposition proposals, each
// attached to an EXACT, VALIDATED anchor. Each proposal is PARKED as an attorney review item through the
// shared LPI integration repository and linked to its anchor. It never scores, applies, or picks winners.
//
// When the processor reports CapabilityUnavailable, the asset remains available for manual annotation:
// no observations/timestamps/regions are fabricated and no extraction is run.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IMediaEvidenceOrchestrationService
{
    // Register a new asset + its first immutable version: store the bytes privately, compute the content
    // hash, and persist the asset/version metadata (SQL carries only the opaque storage key). No scoring.
    Task<MediaAssetRegistrationResult> RegisterAssetAsync(
        MediaAssetRegistration registration, Stream content, CancellationToken cancellationToken = default);

    // Process one asset version end-to-end up to PARKED, anchored proposition proposals (pre-review).
    Task<MediaProcessingResult> ProcessAsync(
        MediaProcessingRequest request, CancellationToken cancellationToken = default);

    // Manually create one anchored proposition proposal (manual-annotation fallback / unsupported formats).
    // The anchor is validated and persisted, the proposition is parked, and an evidence link is created.
    Task<MediaProposalResult> CreateManualProposalAsync(
        MediaManualProposalRequest request, CancellationToken cancellationToken = default);
}

// Request to process one stored asset version.
public sealed record MediaProcessingRequest(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    Guid MediaAssetId,
    Guid MediaAssetVersionId,
    MediaAssetType AssetType,
    string CorrelationId);

// The outcome of processing one asset version.
public sealed record MediaProcessingResult(
    Guid MediaProcessingRunId,
    MediaProcessingStatus Status,
    int ProposalsParked,
    IReadOnlyList<Guid> ParkedPropositionIds,
    string? CoverageNote,
    string StatusCode,          // Processed | CapabilityUnavailable | NoProposals | ExtractionFailed | Failed
    string? Explanation);

// A manually authored anchored proposition (manual fallback). The anchor fields relevant to AnchorType
// must be supplied; bounds are validated against the asset version before persistence.
public sealed record MediaManualProposalRequest(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    Guid MediaAssetVersionId,
    EvidenceAnchorType AnchorType,
    MediaEvidenceKind EvidenceKind,
    string PropositionText,
    LpiAssertionType AssertionType,
    string? AttributedTo,
    DateTimeOffset? EffectiveAt,
    // Anchor payload (only the fields relevant to AnchorType are read).
    decimal? NormX, decimal? NormY, decimal? NormWidth, decimal? NormHeight,
    long? StartMs, long? EndMs, long? FrameNumber, string? SpeakerLabel, string? TranscriptSegmentRef,
    string? RecordReferenceJson);

public sealed record MediaProposalResult(
    Guid RetrievedPropositionId,
    Guid EvidenceAnchorId,
    Guid PropositionEvidenceLinkId,
    string StatusCode,          // Parked | InvalidAnchor
    string? Explanation);
