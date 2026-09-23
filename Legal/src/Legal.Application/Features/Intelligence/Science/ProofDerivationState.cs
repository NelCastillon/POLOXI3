namespace Legal.Application.Features.Intelligence.Science;

// ── Proof / Derivation State (additive; not yet wired) ─────────────────────────────────────────────
// The CENTRAL artifact of scientific reasoning — the analog of the Research pack's Evidence State.
// It answers "what follows necessarily from the assumptions?" rather than "what does the evidence
// support?". It holds the proof dependency graph, its obligations, competing candidates, and keeps
// DISCOVERY confidence separate from VERIFICATION status at all times.
public sealed record ProofDerivationState
{
    public required ScientificProblemContract Contract { get; init; }

    public IReadOnlyList<ProofNode> Nodes { get; init; } = [];

    public IReadOnlyList<ProofEdge> Edges { get; init; } = [];

    public IReadOnlyList<ProofObligation> Obligations { get; init; } = [];

    // Competing resolution objects (proof strategies, lemmas, counterexamples, models, answers).
    public IReadOnlyList<ScientificCandidate> Candidates { get; init; } = [];

    // Explicit claims made by the solution (e.g. "known theorem excludes R", "genuine reduction"), each
    // linked to the obligations it depends on. The certainty-ceiling pass reads this to detect a claim
    // whose asserted strength outruns the verification status of its essential obligations.
    public IReadOnlyList<MathClaim> Claims { get; init; } = [];
    // The current best answer in canonical, comparable form; null until a derivation produces one.
    public string? CanonicalAnswer { get; init; }

    // How promising the current leading direction is (0..1). NEVER interpret as "proven".
    public double DiscoveryConfidence { get; init; }

    // The independent verification result — the only thing that authorizes a PROVEN/DISPROVEN outcome.
    public VerificationStatus VerificationStatus { get; init; } = VerificationStatus.Unverified;

    // Multidimensional epistemic state (R/E/V/F) for the leading direction, kept separate from the single
    // DiscoveryConfidence scalar so research priority is never mistaken for mathematical verification.
    public EpistemicState Epistemic { get; init; } = EpistemicState.Empty;

    // Final convergence classification once the engine stops.
    public ScientificOutcome Outcome { get; init; } = ScientificOutcome.Unresolved;
}

// A competing object in POLOXI candidate competition. In science the thing being resolved is not always
// an "answer": it can be a proof strategy, a lemma, a model, or a counterexample. ResolutionObjectType
// generalizes the engine so the same competition machinery works across domains.
public sealed record ScientificCandidate
{
    public required string Id { get; init; }

    public required ResolutionObjectType ObjectType { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    // The precise mathematical object type this candidate represents (theorem, lemma, property, criterion,
    // descriptor, …). Typing objects prevents e.g. a mere DESCRIPTOR being treated as a REGULARITY_CRITERION,
    // which have completely different proof roles.
    public MathematicalObjectType MathObjectType { get; init; } = MathematicalObjectType.Unspecified;

    // Prediction of usefulness (0..1) — always differentiated, never a claim of correctness.
    public double DiscoveryConfidence { get; init; }

    // Verification status of this candidate, kept separate from its discovery confidence.
    public VerificationStatus VerificationStatus { get; init; } = VerificationStatus.Unverified;

    // Multidimensional epistemic state (R/E/V/F) for this candidate.
    public EpistemicState Epistemic { get; init; } = EpistemicState.Empty;
}

// A claim asserted by the solution, linked to the obligations it depends on. The certainty-ceiling pass
// bounds the strength a claim may be reported at by the verification status of its ESSENTIAL obligations:
//   ReportedStrength(C) <= min over essential O of Verification(O).
// This is what stops "known theorem excludes R" being emitted while its supporting obligation is OPEN.
public sealed record MathClaim
{
    public required string ClaimId { get; init; }

    // The assertion in precise notation (e.g. "CKN excludes axis-region R").
    public required string Statement { get; init; }

    // How strong the SOLUTION wants to state this claim, before the ceiling is applied.
    public ClaimStrength AssertedStrength { get; init; } = ClaimStrength.Hypothesis;

    // The obligations this claim depends on. Essential ones gate the ceiling; supporting ones inform only.
    public IReadOnlyList<string> EssentialObligationIds { get; init; } = [];

    public IReadOnlyList<string> SupportingObligationIds { get; init; } = [];

    // Known theorems this claim invokes (theorem → claim mapping); populated by the prior-art gate.
    public IReadOnlyList<string> CitedTheoremIds { get; init; } = [];
}

// The strength at which a claim may be communicated. Ordered weakest→strongest so the ceiling can take a
// minimum. STRONGLY_SUGGESTED and below never assert mathematical truth; VERIFIED/EXCLUDED do.
public enum ClaimStrength
{
    Speculative,
    Hypothesis,
    StronglySuggested,
    Conditional,
    Verified,
    Excluded,
}

// The kinds of thing POLOXI can resolve — the key generalization that lets one engine span sciences.
public enum ResolutionObjectType
{
    Answer,
    Candidate,
    ProofStrategy,
    Lemma,
    Model,
    Hypothesis,
    Counterexample,
    Parameter,
    Mechanism,
}

// Verification status is categorical and orthogonal to discovery confidence. A theorem is never
// "probably proven at 87%".
public enum VerificationStatus
{
    Unverified,
    Conditional,
    Verified,
    Refuted,
}

// Explicit mathematical object typing (P1 #candidate-type-system). Distinguishing these prevents mixing
// objects with completely different proof roles — e.g. a "scale-dependent energy spectrum" DESCRIPTOR is
// not a REGULARITY_CRITERION even though both can be proposed as candidates.
public enum MathematicalObjectType
{
    Unspecified,
    Theorem,
    Lemma,
    Conjecture,
    Property,
    RegularityCriterion,
    NecessaryCondition,
    SufficientCondition,
    Mechanism,
    Scenario,
    Taxonomy,
    StructuralAxis,
    Invariant,
    Bound,
    Recurrence,
    DynamicalRelation,
    Counterexample,
    Analogy,
    Descriptor,
}