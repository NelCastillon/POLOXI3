namespace Legal.Application.Features.Intelligence.Science;

// ── Proof Obligations (additive; not yet wired) ────────────────────────────────────────────────────
// Every proposed inferential step generates obligations that POLOXI must not simply accept. If an LLM
// proposes A ⇒ B ⇒ C, we create obligations: does A really imply B? does B imply C? are hidden
// assumptions required? are there boundary cases where the implication fails? Deterministic C#-only
// verification resolves the checkable ones; the rest stay OPEN, never silently "proven".
public sealed record ProofObligation
{
    public required string ObligationId { get; init; }

    // The proof node whose step this obligation guards.
    public string? ParentProofNodeId { get; init; }

    // Exactly what must hold, rewritten as an explicit equation, inequality, or predicate.
    public required string Statement { get; init; }

    public required ObligationVerificationMethod VerificationMethod { get; init; }

    public ObligationStatus Status { get; init; } = ObligationStatus.Open;

    // Whether a deterministic C# check found a counterexample to this obligation.
    public CounterexampleStatus CounterexampleStatus { get; init; } = CounterexampleStatus.NotSearched;

    // Discovery confidence (how plausible the step looks) — kept STRICTLY separate from Status, which
    // is the verification result. A high confidence never upgrades an OPEN obligation.
    public double DiscoveryConfidence { get; init; }

    // Human/audit note explaining the verification outcome.
    public string? VerificationNote { get; init; }

    // Whether this obligation is ESSENTIAL to the claim(s) that depend on it. An essential obligation
    // that is not VERIFIED caps the verification strength of every claim that rests on it (the certainty
    // ceiling). Non-essential (supporting) obligations inform discovery but never gate a claim.
    public bool IsEssential { get; init; } = true;
}

public enum ObligationStatus
{
    Open,
    Verified,
    Refuted,
    Conditional,
    Unresolved,
}

public enum ObligationVerificationMethod
{
    Algebraic,
    Numeric,
    SymbolicIdentity,
    CaseExhaustion,
    CounterexampleCheck,
    Logical,
}

public enum CounterexampleStatus
{
    NotSearched,
    NoneFound,
    Found,
}
