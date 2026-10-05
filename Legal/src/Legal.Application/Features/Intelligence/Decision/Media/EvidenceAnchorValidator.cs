using Legal.Application.Features.Intelligence.Decision.Media;

namespace Legal.Application.Features.Intelligence.Decision.Media;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Pure, DB-free validation of an evidence anchor against its asset version. Enforces type-specific
// bounds BEFORE an anchor is persisted or an evidence link is created — the spec requires every anchor
// be validated against the original asset before review and again before integration. Tenant/matter
// ownership is enforced separately by the caller (the version is already tenant-scoped on load).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class EvidenceAnchorValidator
{
    public static bool TryValidate(
        EvidenceAnchorType anchorType,
        MediaAssetVersion version,
        decimal? normX, decimal? normY, decimal? normWidth, decimal? normHeight,
        long? startMs, long? endMs, long? frameNumber,
        string? recordReferenceJson,
        out string? error)
    {
        error = null;
        switch (anchorType)
        {
            case EvidenceAnchorType.ImageRegion:
                if (normX is null || normY is null || normWidth is null || normHeight is null)
                {
                    error = "An image-region anchor requires normX, normY, normWidth, and normHeight.";
                    return false;
                }
                if (!InUnit(normX.Value) || !InUnit(normY.Value) || !InUnit(normWidth.Value) || !InUnit(normHeight.Value))
                {
                    error = "Image-region coordinates must be normalized within [0,1].";
                    return false;
                }
                if (normX.Value + normWidth.Value > 1.0000001m || normY.Value + normHeight.Value > 1.0000001m)
                {
                    error = "The image region extends outside the image bounds.";
                    return false;
                }
                if (normWidth.Value <= 0 || normHeight.Value <= 0)
                {
                    error = "The image region must have a positive width and height.";
                    return false;
                }
                return true;

            case EvidenceAnchorType.VideoInterval:
            case EvidenceAnchorType.AudioInterval:
                if (startMs is null || endMs is null)
                {
                    error = "A media-interval anchor requires startMs and endMs.";
                    return false;
                }
                if (startMs.Value < 0 || endMs.Value < startMs.Value)
                {
                    error = "The media interval must have startMs >= 0 and endMs >= startMs.";
                    return false;
                }
                // When a duration is known, the interval must fall within it.
                if (version.DurationMs is { } duration && endMs.Value > duration)
                {
                    error = $"The media interval end ({endMs.Value} ms) exceeds the asset duration ({duration} ms).";
                    return false;
                }
                if (anchorType == EvidenceAnchorType.VideoInterval && frameNumber is < 0)
                {
                    error = "The frame number must be non-negative.";
                    return false;
                }
                return true;

            case EvidenceAnchorType.StructuredRecord:
                if (string.IsNullOrWhiteSpace(recordReferenceJson))
                {
                    error = "A structured-record anchor requires a record reference.";
                    return false;
                }
                return true;

            default:
                error = "Unknown anchor type.";
                return false;
        }
    }

    private static bool InUnit(decimal value) => value >= 0m && value <= 1m;
}
