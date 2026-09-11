namespace Legal.Application.Features.Intelligence.Science;

// ── POLOXI Scientific Reasoning — Mathematics V1 (additive; not yet wired) ─────────────────────────
// The structured contract produced BEFORE any solving is attempted. This is the scientific counterpart
// to the Research pipeline's query contract: instead of describing what to investigate, it describes
// what must follow necessarily from the assumptions. Mathematics is the foundational domain pack;
// the same shape is designed to be reused by future Physics/Chemistry packs via ScientificDomain.
public sealed record ScientificProblemContract
{
    // Restated, precise formal claim or task in a single sentence.
    public required string Statement { get; init; }

    public required ScientificProblemType ProblemType { get; init; }

    // Foundational pack is MATHEMATICS; kept as an enum so Physics/Chemistry/etc. plug in later.
    public ScientificDomain Domain { get; init; } = ScientificDomain.Mathematics;

    // Most specific area, e.g. NUMBER_THEORY, REAL_ANALYSIS, COMBINATORICS. Free-form by design.
    public string? Subdomain { get; init; }

    public IReadOnlyList<string> Givens { get; init; } = [];

    public IReadOnlyList<string> Unknowns { get; init; } = [];

    // Implicit conditions made explicit (e.g. "n is a positive integer").
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    // Domain/range/integrality/positivity limits.
    public IReadOnlyList<string> Constraints { get; init; } = [];

    // Any non-standard terms the problem defines.
    public IReadOnlyList<string> Definitions { get; init; } = [];

    // Universal/existential structure of the claim.
    public IReadOnlyList<string> Quantifiers { get; init; } = [];

    // The exact quantity, statement, or object to resolve.
    public required string Target { get; init; }

    // Techniques the problem restricts to; empty means unrestricted.
    public IReadOnlyList<string> AllowedMethods { get; init; } = [];

    public bool RequiresExternalKnowledge { get; init; }

    public bool RequiresSymbolicComputation { get; init; }

    public bool RequiresNumericalComputation { get; init; }

    // Whether a formal proof (not just numeric/case checks) is expected for acceptance.
    public bool RequiresFormalVerification { get; init; }
}

// Classifies the problem before solving. Broad enough to cover conjectures and constructive tasks,
// not just "answer a question".
public enum ScientificProblemType
{
    Prove,
    Disprove,
    Conjecture,
    Derive,
    Compute,
    Solve,
    Optimize,
    Classify,
    Construct,
    Existence,
    Uniqueness,
    Bound,
    Approximate,
    Model,
    Simulate,
    Verify,
    Explain,
}

// Foundational pack first; the rest are placeholders so the engine stays composable.
public enum ScientificDomain
{
    Mathematics,
    Physics,
    Chemistry,
    TheoreticalComputerScience,
    Statistics,
}
