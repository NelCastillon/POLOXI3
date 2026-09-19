using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — lightweight decision-support model (frozen slice-1 design).
//
// A DecisionSupportSignal is a single, verifiable statement that a branch/candidate depends on:
// "this material fact/authority, if verified, moves the outcome by this much." It is deliberately
// lighter than the full EA claim-authority subsystem — it reuses the existing Candidate × Branch
// recompetition engine rather than introducing a second reasoning authority.
//
// CORE INVARIANT (the entire point of this layer):
//   A signal that REQUIRES verification and is NOT in the Supported state contributes ZERO positive
//   support. POLOXI never manufactures authority from model confidence. Contradicted signals may
//   contribute a NEGATIVE delta (contradiction is meaningful evidence, not erased). Signals with no
//   branch/candidate lineage produce nothing (they cannot silently move the ranking).
//
// The existing EpistemicDecisionBridge (EA-7) remains advisory/diagnostic only; this path is the
// lightweight, deterministic integration surface for decision support.
// ─────────────────────────────────────────────────────────────────────────────────────────────

// Where the signal came from, so we never treat unverified model output as authoritative.
public enum DecisionSupportOrigin
{
    LlmGenerated = 0,
    HumanAsserted = 1,
    RetrievedEvidence = 2,
}

// The verification lifecycle of a support signal. Only Supported permits positive contribution when
// verification is required.
public enum DecisionSupportVerificationState
{
    Unverified = 0,
    Supported = 1,
    Contradicted = 2,
    Disputed = 3,
}

// A single verifiable support signal keyed by its authoritative branch/candidate lineage.
public sealed record DecisionSupportSignal
{
    public Guid SignalId { get; init; } = Guid.NewGuid();
    public Guid DecisionSessionId { get; init; }
    public Guid? MatterId { get; init; }

    public required string Statement { get; init; }
    public required string NormalizedStatement { get; init; }

    public DecisionSupportOrigin Origin { get; init; } = DecisionSupportOrigin.LlmGenerated;
    public DecisionSupportVerificationState VerificationState { get; init; } = DecisionSupportVerificationState.Unverified;

    // When true, the signal is material to the outcome and MUST be verified before it may contribute
    // positive support. Immaterial/non-required signals never gate the decision.
    public bool RequiresVerification { get; init; } = true;

    // Independent verification confidence in [0,1]; only meaningful when Supported.
    public decimal VerificationStrength { get; init; }

    // How much the outcome moves if this signal holds, in [0,1].
    public decimal DecisionImpact { get; init; }

    // Authoritative lineage: at least one of these must be set for the signal to emit anything.
    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }

    public string? ProposedByModel { get; init; }
    public string? PromptRunId { get; init; }
    public string? VerificationReason { get; init; }
}

// Stable string codes used at the persistence boundary (mirrors the *Code columns in the table).
public static class DecisionSupportSignalCodes
{
    public static string ToCode(DecisionSupportOrigin origin) => origin switch
    {
        DecisionSupportOrigin.HumanAsserted => "HumanAsserted",
        DecisionSupportOrigin.RetrievedEvidence => "RetrievedEvidence",
        _ => "LlmGenerated",
    };

    public static DecisionSupportOrigin ToOrigin(string? code) => code switch
    {
        "HumanAsserted" => DecisionSupportOrigin.HumanAsserted,
        "RetrievedEvidence" => DecisionSupportOrigin.RetrievedEvidence,
        _ => DecisionSupportOrigin.LlmGenerated,
    };

    public static string ToCode(DecisionSupportVerificationState state) => state switch
    {
        DecisionSupportVerificationState.Supported => "Supported",
        DecisionSupportVerificationState.Contradicted => "Contradicted",
        DecisionSupportVerificationState.Disputed => "Disputed",
        _ => "Unverified",
    };

    public static DecisionSupportVerificationState ToState(string? code) => code switch
    {
        "Supported" => DecisionSupportVerificationState.Supported,
        "Contradicted" => DecisionSupportVerificationState.Contradicted,
        "Disputed" => DecisionSupportVerificationState.Disputed,
        _ => DecisionSupportVerificationState.Unverified,
    };
}

// A flat persistence row for a decision support signal (mirrors POLOXI.Legal_DecisionSupportSignal).
public sealed record DecisionSupportSignalPersistence(
    Guid SignalId,
    Guid DecisionSessionId,
    Guid? MatterId,
    string Statement,
    string NormalizedStatement,
    string OriginCode,
    string VerificationStateCode,
    bool RequiresVerification,
    decimal VerificationStrength,
    decimal DecisionImpact,
    Guid? SourceBranchId,
    Guid? SourceCandidateId,
    string? ProposedByModel,
    string? PromptRunId,
    string? VerificationReason,
    Guid TenantId,
    Guid? ActorUserId);
