namespace Legal.Application.Features.Intelligence.Science;

// ── C#-only deterministic verification seam (additive; not yet wired) ──────────────────────────────
// POLOXI separates DISCOVERY (the LLM proposes) from VERIFICATION (deterministic C# decides).
// Per the design constraint, verification is C# only — no external CAS/Python. This seam owns the
// acceptance authority: an obligation is only VERIFIED/REFUTED here, never on LLM confidence.
public interface IMathVerifier
{
    // Deterministically check a single obligation. Returns the obligation with an updated Status and
    // CounterexampleStatus. Obligations that cannot be reduced to a deterministic check stay OPEN.
    ProofObligation Verify(ProofObligation obligation, MathVerificationRequest request);

    // Normalize a raw answer string into a canonical form suitable for exact equality comparison
    // (reduce fractions, trim insignificant whitespace, standardize casing for booleans/statements).
    string Canonicalize(string rawAnswer, ScientificAnswerType answerType);

    // Aggregate independently derived candidate answers by canonical equality (self-consistency).
    SelfConsistencyResult Aggregate(IReadOnlyList<CandidateAnswer> candidateAnswers);
}

// A checkable claim reduced to an explicit relation over concrete inputs.
public sealed record MathVerificationRequest
{
    public required string NormalizedClaim { get; init; }

    public ObligationVerificationMethod Method { get; init; }

    public ExpectedRelation ExpectedRelation { get; init; } = ExpectedRelation.None;

    // Concrete values/cases to evaluate for Numeric/CaseExhaustion/CounterexampleCheck methods.
    public IReadOnlyList<double> TestInputs { get; init; } = [];

    // Left/right numeric operands when the claim is a direct scalar relation (e.g. an arithmetic step).
    public double? Left { get; init; }

    public double? Right { get; init; }

    // Absolute tolerance for numeric comparisons.
    public double Tolerance { get; init; } = 1e-9;
}

public sealed record CandidateAnswer
{
    public required string StrategyName { get; init; }

    public required string CanonicalForm { get; init; }
}

public sealed record SelfConsistencyResult
{
    public required IReadOnlyList<AnswerCluster> Clusters { get; init; }

    public string? PluralityCanonicalForm { get; init; }

    // Fraction of candidates in the plurality cluster (0..1). Raises DISCOVERY confidence only —
    // agreement is NOT verification.
    public double AgreementRatio { get; init; }
}

public sealed record AnswerCluster
{
    public required string CanonicalForm { get; init; }

    public required int MemberCount { get; init; }

    public required IReadOnlyList<string> StrategyNames { get; init; }
}

public enum ExpectedRelation
{
    None,
    Equal,
    LessThan,
    GreaterThan,
    HoldsForAll,
    Exists,
}

public enum ScientificAnswerType
{
    Numeric,
    ExactFraction,
    Expression,
    Set,
    Tuple,
    Boolean,
    Statement,
}
