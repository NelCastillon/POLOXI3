using Legal.Application;

namespace Legal.Application.Features.Intelligence.Science;

// ── POLOXI Formalization Gate — Research → Formalize → Math handoff contracts ─────────────────────────
// The Formalization Gate is a real reasoning stage (not prompt formatting): it converts a surviving
// RESEARCH idea/hypothesis into ONE clean mathematical Proof Contract of the form Assumptions ⇒ Claim
// that the Math Solver can attack directly. The LLM only PROPOSES the contract; nothing here is proven.
//
//   Research → [Formalize] → Math Solver → Verifier → Research update
//
// The produced ProofContract is the handoff object the Math Solver should receive — never the raw
// 150-line research narrative.

// Public service request: the surviving research idea/hypothesis to formalize, plus optional context.
public sealed record FormalizationRequest(
    Guid TenantId,
    Guid UserId,
    string ResearchIdea,
    string CorrelationId = "")
{
    // Optional broader research context (evidence, prior art, candidate comparison) that helps the
    // formalizer pin down assumptions and forbidden circular assumptions.
    public string? ResearchContext { get; init; }

    // Optional model routing override (e.g. a reasoning model for the formalization stage).
    public string? ModelCode { get; init; }

    public IReadOnlyCollection<string> GrantedPermissions { get; init; } = [];
}

// Public service response: the structured Proof Contract handoff plus routing/degradation metadata.
public sealed record FormalizationResponse
{
    public required ProofContract Contract { get; init; }

    // A ready-to-solve problem statement composed from the contract (statement + assumptions + forbidden
    // assumptions + falsification), suitable to hand directly to the Math Solver.
    public required string MathSolverHandoff { get; init; }

    // False when the AI model route was unavailable and the gate degraded to an echo contract. The UI
    // uses this to surface an actionable configuration message.
    public bool ModelAvailable { get; init; } = true;

    public required string CorrelationId { get; init; }
}

// MATH_FORMALIZATION_GATE — the Proof Contract handoff object (tolerant LLM-shaped DTO). Arrays default
// to empty; strings are nullable so a partial formalization still deserializes and degrades honestly.
public sealed record ProofContract
{
    public string? CandidateId { get; init; }

    // THEOREM_CANDIDATE / LEMMA_CANDIDATE / PROPERTY_CANDIDATE / REGULARITY_CRITERION /
    // NECESSARY_CONDITION / SUFFICIENT_CONDITION / BOUND_CANDIDATE / COUNTEREXAMPLE_TARGET /
    // REDUCTION_TARGET / CONJECTURE.
    public string? ObjectType { get; init; }

    // The precise formal claim in one restated, fully-quantified mathematical sentence.
    public string? Statement { get; init; }

    // The explicit hypotheses under which the claim is asserted (Assumptions ⇒ Claim).
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> Definitions { get; init; } = [];

    // Prior results / lemmas / known theorems the claim relies on.
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public string? Target { get; init; }

    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    // Assumptions that would make the argument circular or trivial (e.g. assuming the conclusion).
    public IReadOnlyList<string> ForbiddenAssumptions { get; init; } = [];

    // What precisely counts as a proof of this claim.
    public string? ProofStandard { get; init; }

    // A concrete condition/construction that would DISPROVE the claim.
    public string? Falsification { get; init; }

    // The verdicts the Math Solver may return (subset of PROVED/DISPROVED/COUNTEREXAMPLE/
    // REDUCED_TO_LEMMAS/INCONCLUSIVE).
    public IReadOnlyList<string> ExpectedReturns { get; init; } = [];

    // How well-posed and attackable the formalization is (0..1) — NOT the probability the claim is true.
    public double Confidence { get; init; }
}

// JSON output schema for the formalization stage, mirroring the MathContractSchemas.*Schema style.
public static class FormalizationContractSchemas
{
    public const string ProofContractSchema = """
{
  "type": "object",
  "properties": {
    "candidateId": { "type": "string" },
    "objectType": { "type": "string", "enum": ["THEOREM_CANDIDATE", "LEMMA_CANDIDATE", "PROPERTY_CANDIDATE", "REGULARITY_CRITERION", "NECESSARY_CONDITION", "SUFFICIENT_CONDITION", "BOUND_CANDIDATE", "COUNTEREXAMPLE_TARGET", "REDUCTION_TARGET", "CONJECTURE"] },
    "statement": { "type": "string" },
    "assumptions": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
    "definitions": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
    "dependencies": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
    "target": { "type": "string" },
    "allowedTools": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
    "forbiddenAssumptions": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
    "proofStandard": { "type": ["string", "null"] },
    "falsification": { "type": ["string", "null"] },
    "expectedReturns": { "type": "array", "maxItems": 5, "items": { "type": "string", "enum": ["PROVED", "DISPROVED", "COUNTEREXAMPLE", "REDUCED_TO_LEMMAS", "INCONCLUSIVE"] } },
    "confidence": { "type": "number" }
  },
  "required": ["candidateId", "objectType", "statement", "assumptions", "definitions", "dependencies", "target", "allowedTools", "forbiddenAssumptions", "proofStandard", "falsification", "expectedReturns", "confidence"],
  "additionalProperties": false
}
""";

    // Short stage directive, mirroring MathPromptContracts entries.
    public const string Directive =
        "Convert the surviving research idea into ONE clean, self-contained Proof Contract of the form Assumptions => Claim. Restate the claim as a precise, fully-quantified mathematical sentence with no hedging words. Make assumptions, definitions, dependencies, allowed tools, and forbidden (circular) assumptions explicit, and state what counts as proof and what would falsify it. Never solve the problem and never smuggle the conclusion into the assumptions.";
}
