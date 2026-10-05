namespace Legal.Application.Features.Intelligence.Decision.Media;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Media & Machine Evidence contracts (Phase 3, Slice 1).
//
// These records model the asset/version/derivative/anchor/link/run domain for the THIRD evidence
// channel (channel code MEDIA_MACHINE_DATA). They back migration 0401. Media supplies new atomic
// propositions anchored to exact source regions/intervals; it NEVER scores candidates or picks a
// winner. Accepted media propositions converge on the SHARED IPropositionIntegrationService funnel
// (reusing RetrievedProposition + LpiPlacementProposal), exactly like Document Retrieval.
//
// INVARIANTS:
//   * A content hash identifies BYTES; it does NOT prove authenticity.
//   * Every anchor/link stays within one tenant AND one matter.
//   * Media-relative time (interval ms) is distinct from asserted real-world event time.
//   * Processors never fabricate observations, timestamps, or regions; unsupported capability is
//     reported explicitly (CapabilityUnavailable) and the reviewer annotates manually.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public static class MediaEvidenceChannel
{
    // Registered channel code for this evidence source.
    public const string ChannelCode = "MEDIA_MACHINE_DATA";
}

// Supported asset types. Slice 1 processes Photo + Audio end-to-end; the rest are schema-ready and
// available for manual annotation until their processors ship.
public enum MediaAssetType
{
    Photo,
    Video,
    Audio,
    Gps,
    Telematics,
    DeviceLog,
    SystemLog
}

// Precise, type-specific source location kinds.
public enum EvidenceAnchorType
{
    ImageRegion,
    VideoInterval,
    AudioInterval,
    StructuredRecord
}

// How a media-derived proposal relates to the anchored source content.
public enum MediaEvidenceKind
{
    Observation,            // directly observable content
    AttributedAssertion,    // a spoken/written assertion attributed to someone
    ProposedInterpretation  // an inference proposed from the material
}

// Kinds of generated derivative artifacts.
public enum MediaDerivativeType
{
    Thumbnail,
    Preview,
    Transcript,
    Frame,
    ParsedLog
}

// Processing-run lifecycle. CapabilityUnavailable means the configured provider cannot perform the
// required analysis (e.g. no vision/speech) — the reviewer annotates manually; nothing is fabricated.
public enum MediaProcessingStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    CapabilityUnavailable
}

// Reviewer confirmation state of a proposition↔anchor link.
public enum EvidenceLinkReviewStatus
{
    Pending,
    Confirmed,
    Rejected
}

// A logical, versioned media/machine asset registered against a matter.
public sealed record MediaAsset(
    Guid MediaAssetId,
    Guid TenantId,
    Guid DecisionMatterId,
    Guid? MatterResourceId,
    MediaAssetType AssetType,
    string OriginalFileName,
    string MimeType,
    string? SourceDescription,
    Guid? UploadedByUserId,
    DateTimeOffset CreatedDateUtc);

// An immutable version of an asset's bytes + metadata + capture assertion. Bytes live in the binary
// store; SQL carries only the opaque StorageKey and hash.
public sealed record MediaAssetVersion(
    Guid MediaAssetVersionId,
    Guid MediaAssetId,
    int VersionNumber,
    string StorageKey,
    string ContentHash,
    long ByteLength,
    string? MetadataJson,
    DateTimeOffset? CaptureAssertedAtUtc,
    DateTimeOffset IngestedAtUtc,
    long? DurationMs,
    int? WidthPx,
    int? HeightPx);

// A generated artifact (thumbnail/preview/transcript/frame/parsed log) of an asset version.
public sealed record MediaDerivative(
    Guid MediaDerivativeId,
    Guid MediaAssetVersionId,
    Guid? MediaProcessingRunId,
    MediaDerivativeType DerivativeType,
    string? StorageKey,
    string? MimeType,
    string? PayloadJson);

// A precise source anchor within an asset version. Only the fields relevant to AnchorType are set.
public sealed record EvidenceAnchor(
    Guid EvidenceAnchorId,
    Guid TenantId,
    Guid DecisionMatterId,
    Guid MediaAssetVersionId,
    EvidenceAnchorType AnchorType,
    // IMAGE_REGION (normalized [0,1]).
    decimal? NormX,
    decimal? NormY,
    decimal? NormWidth,
    decimal? NormHeight,
    // VIDEO_INTERVAL / AUDIO_INTERVAL (media-relative milliseconds).
    long? StartMs,
    long? EndMs,
    long? FrameNumber,
    string? SpeakerLabel,
    string? TranscriptSegmentRef,
    // STRUCTURED_RECORD (record ids / row range / fields / unit / coordinate reference as JSON).
    string? RecordReferenceJson);

// Binds a parked/accepted proposition (Legal_RetrievedProposition) to an anchor + relationship + run.
public sealed record PropositionEvidenceLink(
    Guid PropositionEvidenceLinkId,
    Guid TenantId,
    Guid DecisionMatterId,
    Guid RetrievedPropositionId,
    Guid EvidenceAnchorId,
    MediaEvidenceKind EvidenceKind,
    Guid? MediaProcessingRunId,
    EvidenceLinkReviewStatus ReviewStatus);

// A processor invocation over an asset version (idempotent, retry-safe) + status + coverage + outputs.
public sealed record MediaProcessingRun(
    Guid MediaProcessingRunId,
    Guid TenantId,
    Guid DecisionMatterId,
    Guid MediaAssetVersionId,
    string ProcessorCode,
    string? ProcessorVersion,
    MediaProcessingStatus Status,
    string? CoverageJson,
    string? ErrorCode,
    string? ErrorMessage,
    string IdempotencyKey,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);

// ── Request/registration payloads ─────────────────────────────────────────────────────────────────

// Registering a new asset + its first immutable version. Content bytes are supplied separately as a
// stream to the orchestration service, which stores them and computes the hash.
public sealed record MediaAssetRegistration(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    Guid? MatterResourceId,
    MediaAssetType AssetType,
    string OriginalFileName,
    string MimeType,
    string? SourceDescription,
    string? MetadataJson,
    DateTimeOffset? CaptureAssertedAtUtc,
    long? DurationMs,
    int? WidthPx,
    int? HeightPx);

// The result of registering/storing an asset version.
public sealed record MediaAssetRegistrationResult(
    Guid MediaAssetId,
    Guid MediaAssetVersionId,
    int VersionNumber,
    string ContentHash,
    long ByteLength);
