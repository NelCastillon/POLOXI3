namespace Legal.Application.Features.Intelligence.Science;

// ── Proof Dependency Graph (additive; not yet wired) ───────────────────────────────────────────────
// Mathematics needs a graph the Research pack does not: the explicit dependency structure of a proof.
// Formally, if L1 ∧ L2 ∧ C3 ⇒ T, every proof claim tracks what it rests on. This lets POLOXI compute
// proof criticality (if this node fails, how much collapses?) and target verification compute there.
public sealed record ProofNode
{
    public required string Id { get; init; }

    public required ProofNodeType NodeType { get; init; }

    // The claim/step in precise notation.
    public required string Statement { get; init; }

    // Why this node holds (cited theorem, derivation, assumption, etc.).
    public string? Justification { get; init; }

    // How much of the current solution collapses if this node fails (0..1). Verification compute is
    // allocated in proportion to this.
    public double ProofCriticality { get; init; }
}

public sealed record ProofEdge
{
    public required string FromNodeId { get; init; }

    public required string ToNodeId { get; init; }

    public required ProofEdgeRelation Relation { get; init; }
}

public enum ProofNodeType
{
    Axiom,
    Definition,
    Assumption,
    KnownTheorem,
    Lemma,
    Proposition,
    Case,
    Derivation,
    Contradiction,
    Counterexample,
    Conclusion,
}

public enum ProofEdgeRelation
{
    Implies,
    Requires,
    Specializes,
}
