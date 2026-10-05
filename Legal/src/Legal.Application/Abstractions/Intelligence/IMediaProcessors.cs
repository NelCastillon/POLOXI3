using Legal.Application.Features.Intelligence.Decision.Media;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Capability-aware media processors (Phase 3, Slice 1).
//
// Slice 1 ships PHOTO (image analysis) and AUDIO (timestamped transcription) processors. Each processor
// first reports whether the configured provider actually supports the required capability. When a
// capability is UNAVAILABLE, the processor returns an explicit unavailable result and the pipeline
// falls back to manual annotation — it NEVER fabricates observations, timestamps, speaker labels, or
// regions. A processor's output is media-derived content (observations / transcript segments) that the
// media extraction prompt turns into anchored proposition proposals; the processor itself never scores.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Outcome of a processor capability probe / run.
public enum MediaProcessorOutcome
{
    Produced,               // content was produced
    CapabilityUnavailable,  // the configured provider cannot perform this analysis — annotate manually
    Failed                  // an error occurred; the asset remains available for manual annotation
}

// One observation produced from an image: a human-readable description + an optional normalized region.
public sealed record MediaImageObservation(
    string Description,
    decimal? NormX,
    decimal? NormY,
    decimal? NormWidth,
    decimal? NormHeight);

// The result of analyzing one image asset version.
public sealed record MediaImageAnalysisResult(
    MediaProcessorOutcome Outcome,
    IReadOnlyList<MediaImageObservation> Observations,
    // Disclosed coverage/limitations (e.g. "OCR not run", "single still analyzed"). Never over-claims.
    string? CoverageNote,
    string? ProcessorVersion,
    string? ErrorCode,
    string? ErrorMessage);

// One time-aligned transcript segment. Start/End are media-relative milliseconds (NOT real-world time).
// SpeakerLabel is tentative until a reviewer confirms it.
public sealed record MediaTranscriptSegment(
    long StartMs,
    long EndMs,
    string Text,
    string? SpeakerLabel,
    string SegmentRef);

// The result of transcribing one audio asset version.
public sealed record MediaAudioTranscriptionResult(
    MediaProcessorOutcome Outcome,
    IReadOnlyList<MediaTranscriptSegment> Segments,
    // Disclosed coverage (e.g. duration transcribed vs total; speaker diarization confidence). Honest.
    string? CoverageNote,
    string? ProcessorVersion,
    string? ErrorCode,
    string? ErrorMessage);

// Analyzes still images (PHOTO). Implementations detect provider support and report it explicitly.
public interface IMediaImageAnalyzer
{
    bool IsSupported { get; }

    Task<MediaImageAnalysisResult> AnalyzeAsync(
        Guid tenantId,
        MediaAssetVersion version,
        Stream imageContent,
        string correlationId,
        CancellationToken cancellationToken = default);
}

// Produces time-aligned transcripts from audio (AUDIO). Text-only extraction is NOT a substitute: an
// implementation that cannot timestamp audio must report CapabilityUnavailable.
public interface IMediaAudioTranscriber
{
    bool IsSupported { get; }

    Task<MediaAudioTranscriptionResult> TranscribeAsync(
        Guid tenantId,
        MediaAssetVersion version,
        Stream audioContent,
        string correlationId,
        CancellationToken cancellationToken = default);
}
