using System.Linq;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — LPI (Legal Proposition Intelligence) score INITIALIZER.
//
// Purpose: compute an OPTIONAL ancestor-informed INITIAL score for a newly inserted proposition
// candidate node, WITHOUT changing parent aggregation or lineage propagation. The authoritative
// recompute remains owned by POLOXI Core after commit (§13, §16, §26). This calculator is:
//
//   • Pure / deterministic / side-effect free — every input is explicit and inspectable.
//   • Advisory only — it initializes a candidate-specific starting score; it NEVER verifies a
//     proposition, strengthens evidence, establishes a leader/winner, or grants an origin bonus.
//   • Scale-consistent — the local baseline B_c and ancestor context A_c are both 0–100 relative
//     placement scores (Checkpoint 1), so the blend S = (1-α)·B_c + α·A_c is well defined.
//   • Pre-insertion — ancestor scores are captured BEFORE the new node is inserted and must EXCLUDE
//     the candidate's own node, so there is no self-reference or double counting.
//
// Formula (version LPI_INIT_V1):
//   B_c = local baseline interpolated between comparable neighbor scores at placement fraction t,
//         or the single available neighbor, or (when no comparable neighbor exists) unavailable.
//   A_c = λ-decayed weighted mean of pre-insertion ancestor scores, nearest ancestor weighted highest:
//         weight(depth d, d=1 nearest) = λ^(d-1); A_c = Σ w_d·score_d / Σ w_d.
//   S   = (1-α)·B_c + α·A_c   (only when BOTH B_c and A_c are available).
//
// Fallbacks (never invent a value):
//   • No comparable ancestors            → S = B_c                       (LocalOnly)
//   • No local baseline but ancestors    → S = A_c, flagged review       (AncestorOnlyReviewRequired)
//   • Neither available                  → Uninitialized                 (requires explicit review)
//   • AncestorInfluenceEnabled == false  → S = B_c (existing behavior)    (Disabled)
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public sealed class LpiScoreInitializerOptions
{
    public const string SectionName = "Poloxi:Legal:LpiScoreInitializer";

    // Master switch. Disabled by default so the current (local-only) initialization is preserved
    // byte-for-byte until comparison/regression testing has validated the ancestor-informed path.
    public bool AncestorInfluenceEnabled { get; init; }

    // α — weight given to ancestor context in the blend (0 = local only, 1 = ancestor only).
    public decimal Alpha { get; init; } = 0.2m;

    // λ — per-level decay applied to ancestor weights (0 < λ ≤ 1); nearest ancestor is weighted highest.
    public decimal Lambda { get; init; } = 0.5m;
}

// One pre-insertion ancestor score. Depth is 1 for the immediate parent, 2 for grandparent, etc.
public readonly record struct LpiAncestorScore(Guid NodeId, int Depth, decimal Value, long NodeVersion);

// Explicit, reproducible initializer input. No hidden state — every value is supplied by the caller
// and can be printed/audited for a single original-vs-proposed example.
public sealed record LpiScoreInitializerInput(
    Guid CandidateNodeId,
    long HierarchyRevision,
    decimal? PreviousNeighborScore,
    decimal? NextNeighborScore,
    decimal PlacementFraction,                       // t ∈ [0,1]; 0.5 = midpoint
    IReadOnlyList<LpiAncestorScore> PreInsertionAncestorScores,
    string FormulaVersion = LpiScoreInitializer.FormulaVersionV1);

public enum LpiInitializationMethod
{
    Disabled,                      // ancestor influence off → existing local-only initialization
    LocalOnly,                     // no comparable ancestors → B_c
    Blended,                       // S = (1-α)·B_c + α·A_c
    AncestorOnlyReviewRequired,    // no local baseline → A_c, attorney review required
    Uninitialized                  // neither baseline available → explicit review required
}

public sealed record LpiScoreInitializerResult(
    bool HasScore,
    decimal? InitialScore,                           // the value to use for initialization (null when Uninitialized)
    decimal? LocalBaseline,                          // B_c — the EXISTING local-only value, always shown for comparison
    decimal? AncestorContext,                        // A_c — λ-decayed ancestor mean (null when no ancestors)
    decimal Alpha,
    decimal Lambda,
    string FormulaVersion,
    LpiInitializationMethod Method,
    IReadOnlyList<LpiAncestorScore> AncestorsUsed,   // exactly which ancestors (and versions) fed A_c
    string Explanation);                             // initialization "why", separate from outcome-support changes

public interface ILpiScoreInitializer
{
    // True when ancestor influence is enabled. Callers use this to AVOID the extra pre-insertion
    // ancestor DB read while the feature is off (the default), keeping the existing preview path
    // at its original cost. When false, Compute still returns a correct local-only result.
    bool AncestorInfluenceEnabled { get; }

    // Pure computation of the initial candidate score. Deterministic and side-effect free.
    LpiScoreInitializerResult Compute(LpiScoreInitializerInput input);
}

public sealed class LpiScoreInitializer(LpiScoreInitializerOptions options) : ILpiScoreInitializer
{
    public const string FormulaVersionV1 = "LPI_INIT_V1";

    private readonly LpiScoreInitializerOptions _options = options ?? new LpiScoreInitializerOptions();

    public bool AncestorInfluenceEnabled => _options.AncestorInfluenceEnabled;

    public LpiScoreInitializerResult Compute(LpiScoreInitializerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var alpha = Clamp01(_options.Alpha);
        var lambda = ClampLambda(_options.Lambda);

        // ── Local baseline B_c (the EXISTING behavior) ──────────────────────────────────────────
        var localBaseline = ComputeLocalBaseline(
            input.PreviousNeighborScore, input.NextNeighborScore, input.PlacementFraction);

        // When ancestor influence is disabled, preserve the current local-only initialization exactly.
        if (!_options.AncestorInfluenceEnabled)
        {
            return localBaseline is { } b
                ? new LpiScoreInitializerResult(true, Round(b), Round(b), null, alpha, lambda,
                    input.FormulaVersion, LpiInitializationMethod.Disabled, Array.Empty<LpiAncestorScore>(),
                    $"Ancestor influence disabled; initialized from local placement B_c={Round(b):0.##} only.")
                : new LpiScoreInitializerResult(false, null, null, null, alpha, lambda,
                    input.FormulaVersion, LpiInitializationMethod.Uninitialized, Array.Empty<LpiAncestorScore>(),
                    "Ancestor influence disabled and no comparable neighbor score available; requires explicit attorney review.");
        }

        // ── Ancestor context A_c (pre-insertion, self-excluded, λ-decayed) ──────────────────────
        var ancestors = (input.PreInsertionAncestorScores ?? Array.Empty<LpiAncestorScore>())
            .Where(a => a.NodeId != input.CandidateNodeId)          // never self-reference
            .Where(a => a.Depth >= 1)
            .OrderBy(a => a.Depth)
            .ToArray();

        decimal? ancestorContext = null;
        if (ancestors.Length > 0)
        {
            decimal weightedSum = 0m;
            decimal weightTotal = 0m;
            foreach (var a in ancestors)
            {
                var weight = Pow(lambda, a.Depth - 1);         // nearest ancestor (depth 1) → λ^0 = 1
                weightedSum += weight * a.Value;
                weightTotal += weight;
            }

            if (weightTotal > 0m)
                ancestorContext = weightedSum / weightTotal;
        }

        // ── Blend / fallbacks (never invent) ────────────────────────────────────────────────────
        if (localBaseline is { } local && ancestorContext is { } anc)
        {
            var blended = (1m - alpha) * local + alpha * anc;
            return new LpiScoreInitializerResult(true, Round(blended), Round(local), Round(anc),
                alpha, lambda, input.FormulaVersion, LpiInitializationMethod.Blended, ancestors,
                $"S=(1-α)·B_c+α·A_c = (1-{alpha:0.##})·{Round(local):0.##}+{alpha:0.##}·{Round(anc):0.##} = {Round(blended):0.##} "
                + $"using {ancestors.Length} pre-insertion ancestor(s), λ={lambda:0.##}. Initialization only; POLOXI Core owns the authoritative recompute.");
        }

        if (localBaseline is { } localOnly)
        {
            return new LpiScoreInitializerResult(true, Round(localOnly), Round(localOnly), null,
                alpha, lambda, input.FormulaVersion, LpiInitializationMethod.LocalOnly, Array.Empty<LpiAncestorScore>(),
                $"No comparable pre-insertion ancestor scores; initialized from local placement B_c={Round(localOnly):0.##} only.");
        }

        if (ancestorContext is { } ancOnly)
        {
            return new LpiScoreInitializerResult(true, Round(ancOnly), null, Round(ancOnly),
                alpha, lambda, input.FormulaVersion, LpiInitializationMethod.AncestorOnlyReviewRequired, ancestors,
                $"No comparable neighbor score for a local baseline; provisional A_c={Round(ancOnly):0.##} from {ancestors.Length} ancestor(s). Attorney review required before this initialization is trusted.");
        }

        return new LpiScoreInitializerResult(false, null, null, null, alpha, lambda,
            input.FormulaVersion, LpiInitializationMethod.Uninitialized, Array.Empty<LpiAncestorScore>(),
            "Neither a comparable local baseline nor ancestor context is available; requires explicit attorney review (no value invented).");
    }

    // Local baseline interpolation: both neighbors → interpolate at fraction t; one neighbor → that
    // neighbor; none → unavailable. Mirrors the existing midpoint/bounded/pending placement semantics.
    private static decimal? ComputeLocalBaseline(decimal? previous, decimal? next, decimal placementFraction)
    {
        var t = Clamp01(placementFraction);
        return (previous, next) switch
        {
            ({ } p, { } n) => p + (n - p) * t,
            ({ } p, null) => p,
            (null, { } n) => n,
            _ => null
        };
    }

    private static decimal Pow(decimal baseValue, int exponent)
    {
        if (exponent <= 0) return 1m;
        var result = 1m;
        for (var i = 0; i < exponent; i++) result *= baseValue;
        return result;
    }

    private static decimal Clamp01(decimal value) => Math.Clamp(value, 0m, 1m);

    private static decimal ClampLambda(decimal value) => value <= 0m ? 0.0001m : Math.Min(value, 1m);

    private static decimal Round(decimal value) => Math.Round(Math.Clamp(value, 0m, 100m), 2);
}
