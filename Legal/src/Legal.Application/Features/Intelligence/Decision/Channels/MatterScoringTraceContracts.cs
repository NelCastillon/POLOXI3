namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Matter Scoring Trace — READ-ONLY transparency read model for the Channel Scoring (LPI) workspace.
//
// The channel-contribution trace (ChannelScoringLpiReadModel) only has content once verified channel
// contributions have been folded into a matter's DecisionSession. When a matter was scored through the
// dynamic Wide2 pipeline (its real 40-candidate / L1..Ln branch competition) there is no DecisionSession
// and no channel contribution yet, so that trace is honestly empty.
//
// This model surfaces the SAME persisted Wide execution the page already displays — the authoritative
// hierarchy levels (L1, L2, L3, …), the named branches/candidates, and the exact persisted values
// (EvidenceSupport, PoloxiConfidence, CompositeScore, per-candidate branch EvidenceScore) — so the
// Channel Scoring workspace shows the formulas, values, levels and names that produced the outcome.
// It computes nothing new: every number is read straight from POLOXI.Legal_Wide* rows.
// ────────────────────────────────────────────────────────────────────────────────────────────────

// Top-level read model for one matter's latest Wide execution scoring trace.
public sealed record MatterScoringTraceReadModel(
    Guid MatterId,
    Guid WideExecutionId,
    string? QueryText,
    int DepthReached,
    int CandidateCount,
    decimal EvidenceCoverage,
    decimal FinalConfidence,
    DateTime ExecutedDateUtc,
    MatterScoringFormulaLegend Formula,
    IReadOnlyList<MatterScoringLevel> Levels,
    IReadOnlyList<MatterScoringCandidate> Candidates)
{
    public bool HasTrace => WideExecutionId != Guid.Empty && (Levels.Count > 0 || Candidates.Count > 0);

    public static MatterScoringTraceReadModel Empty(Guid matterId) => new(
        matterId,
        Guid.Empty,
        null,
        0,
        0,
        0m,
        0m,
        default,
        MatterScoringFormulaLegend.Default,
        [],
        []);
}

// The published scoring formulas the Wide pipeline applies, so the UI shows the exact math, not a paraphrase.
public sealed record MatterScoringFormulaLegend(
    string BranchConfidenceFormula,
    string CandidateBranchFormula,
    string CompositeFormula,
    IReadOnlyList<string> Notes)
{
    public static MatterScoringFormulaLegend Default => new(
        BranchConfidenceFormula: "PoloxiConfidence = blend(InterpretationPrior, EvidenceSupport) · gate(BranchRole), clamped to [0,1]",
        CandidateBranchFormula: "Candidate×Branch EvidenceScore ∈ [0,1] = how strongly a candidate competes on that named branch",
        CompositeFormula: "CompositeScore = Σ (branch EvidenceScore × branch weight) + folded channel δ, clamped to [0,1]",
        Notes:
        [
            "Levels L1..Ln are the hierarchy depth of each branch (Legal_WideBranch.LevelNumber).",
            "EvidenceSupport is the retrieved-evidence mass backing a branch; 0 means no admitted evidence yet.",
            "PoloxiConfidence is POLOXI's own confidence in the branch after weighing prior + evidence.",
            "BranchRole (CONTEXT / PREFERENCE / GUARDRAIL) governs how a branch may move candidate ranking.",
            "POLOXI Core alone owns the composite score, margin and winner — this trace only explains the inputs.",
        ]);
}

// One hierarchy level (L1, L2, L3, …) with its named branches and persisted values.
public sealed record MatterScoringLevel(
    int LevelNumber,
    IReadOnlyList<MatterScoringBranch> Branches);

// One named branch at a level, with the exact persisted scoring values.
public sealed record MatterScoringBranch(
    Guid WideBranchId,
    int LevelNumber,
    string DisplayName,
    string? BranchRoleCode,
    string? GroundingStatusCode,
    int EvidenceCount,
    decimal EvidenceSupport,
    decimal PoloxiConfidence,
    decimal Confidence,
    bool IsEliminated,
    string? EliminationReason);

// One competed candidate (a named outcome) with its composite value and per-branch evidence scores.
public sealed record MatterScoringCandidate(
    Guid WideCandidateId,
    int RankNumber,
    string DisplayName,
    decimal CompositeScore,
    bool IsConstraintViolation,
    IReadOnlyList<MatterScoringCandidateBranch> BranchScores);

// One candidate's score on a named branch (the value that feeds the candidate's composite).
public sealed record MatterScoringCandidateBranch(
    string BranchDisplayName,
    decimal EvidenceScore);
