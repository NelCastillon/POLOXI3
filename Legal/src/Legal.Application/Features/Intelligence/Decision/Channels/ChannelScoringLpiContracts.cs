namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Channel Scoring LPI — READ-ONLY transparency read model.
//
// This contract exists purely to SHOW, end-to-end, how each verified channel contribution became a
// typed δ and how that δ was folded into POLOXI's candidate competition. It computes NOTHING new: it
// mirrors the exact classification/formula/lineage rules the live LegalChannelSignalAdapter already
// applies during recompetition (ChannelScoringFormula is the single source of truth for the numbers).
//
// It never asserts a final composite score — POLOXI Core remains the sole owner of candidate ranking.
// Each row reports the applied δ AND, honestly, the rows that had NO effect (unverified / no lineage).
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Top-level read model returned to the UI for one matter's latest decision session.
public sealed record ChannelScoringLpiReadModel(
    Guid MatterId,
    ChannelScoringFormulaLegend Formula,
    IReadOnlyList<ChannelContributionTrace> Contributions,
    IReadOnlyList<ChannelDimensionAggregate> DimensionAggregates,
    int TotalContributions,
    int EffectiveContributions,
    int NoEffectContributions)
{
    public static ChannelScoringLpiReadModel Empty(Guid matterId) => new(
        matterId,
        ChannelScoringFormulaLegend.Default,
        [],
        [],
        0,
        0,
        0);
}

// The published scoring formulas + constants, so the UI shows the exact math, not a paraphrase.
public sealed record ChannelScoringFormulaLegend(
    double SupportMagnitude,
    double QualifiedSupportMagnitude,
    double ContradictionMagnitude,
    string SupportFormula,
    string QualifiedSupportFormula,
    string ContradictionFormula,
    string PlacementFormula,
    IReadOnlyList<string> GatingRules)
{
    public static ChannelScoringFormulaLegend Default => new(
        ChannelScoringFormula.SupportMagnitude,
        ChannelScoringFormula.QualifiedSupportMagnitude,
        ChannelScoringFormula.ContradictionMagnitude,
        SupportFormula: "δ = +0.30 (verified Supports/Establishes)",
        QualifiedSupportFormula: "δ = +0.15 (verified Qualifies)",
        ContradictionFormula: "δ = −0.30 (Contradicts/Invalidates)",
        PlacementFormula: "when a placement magnitude m∈[0,1] is present: δ = 0.15 + (ceiling − 0.15) × m",
        GatingRules:
        [
            "Only VERIFIED Supports/Establishes/Qualifies contributions emit a positive δ.",
            "Contradicts/Invalidates emit a negative δ even against a verified opposing fact.",
            "Challenges request POLOXI to reopen verification (no direct δ).",
            "ContextOnly / Insufficient / Unverified support emit no signal.",
            "No branch/candidate lineage ⇒ no signal: a contribution cannot move ranking without a dependency link.",
            "δ is a signed input in [−1,1]; POLOXI Core alone decides its effect on composite score, margin and winner.",
        ]);
}

// One traced contribution: what it is, the formula applied, the δ value, and its effect (or why none).
public sealed record ChannelContributionTrace(
    Guid ChannelContributionId,
    string ChannelType,
    string Relation,
    string VerificationState,
    string TargetSignalCode,
    string? TargetDimension,
    double? PlacementMagnitude,
    string Effect,
    bool HasLineage,
    double AppliedDelta,
    string FormulaApplied,
    string EffectExplanation,
    string? SourceLabel,
    IReadOnlyList<Guid> AffectedBranchIds,
    IReadOnlyList<Guid> AffectedCandidateIds);

// Per POLOXI input-dimension roll-up of the typed δ's that actually landed, so the UI can show the net
// push each channel dimension exerted on the candidate competition.
public sealed record ChannelDimensionAggregate(
    string Dimension,
    int ContributionCount,
    double NetDelta,
    double PositiveDelta,
    double NegativeDelta);
