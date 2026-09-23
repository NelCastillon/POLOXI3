using System.Text.Json;
using System.Text.Json.Serialization;
using Legal.Application;

namespace Legal.Application.Features.Intelligence.Science;

// ── POLOXI Scientific Reasoning — Mathematics V1 LLM contracts (additive; not yet wired) ────────────
// LLM-shaped response DTOs + JSON output schemas for each MATH_* stage, mirroring the Wide pipeline's
// proposal records + *Schema constants. These are the wire contracts the model must satisfy; they are
// intentionally tolerant (nullable, arrays default to empty) and are mapped into the strongly-typed
// domain records (ScientificProblemContract, ProofNode, ProofObligation, …) by the pipeline.
//
// INVARIANT: the LLM only PROPOSES via these DTOs. Deterministic C# verification owns acceptance, so
// nothing here carries a verification "Status" — that is decided by IMathVerifier downstream.

// MATH_PROBLEM_CONTRACT — classify + structure the problem before any solving.
public sealed record MathProblemContractProposal
{
    public string? Statement { get; init; }
    public string? ProblemType { get; init; }
    public string? Domain { get; init; }
    public string? Subdomain { get; init; }
    public IReadOnlyList<string> Givens { get; init; } = [];
    public IReadOnlyList<string> Unknowns { get; init; } = [];
    public IReadOnlyList<string> Assumptions { get; init; } = [];
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public IReadOnlyList<string> Definitions { get; init; } = [];
    public IReadOnlyList<string> Quantifiers { get; init; } = [];
    public string? Target { get; init; }
    public IReadOnlyList<string> AllowedMethods { get; init; } = [];
    public bool RequiresExternalKnowledge { get; init; }
    public bool RequiresSymbolicComputation { get; init; }
    public bool RequiresNumericalComputation { get; init; }
    public bool RequiresFormalVerification { get; init; }
}

// MATH_STRATEGY_PROPOSAL — competing proof/solution strategies (incl. at least one refutation).
public sealed record MathStrategyProposal
{
    public IReadOnlyList<MathStrategyCandidate> Candidates { get; init; } = [];
}

public sealed record MathStrategyCandidate
{
    public string? Name { get; init; }
    public string? ObjectType { get; init; }
    // Precise mathematical object type (THEOREM, LEMMA, PROPERTY, REGULARITY_CRITERION, DESCRIPTOR, …).
    public string? MathObjectType { get; init; }
    public string? Approach { get; init; }
    public string? ApplicabilityRationale { get; init; }
    public IReadOnlyList<string> RequiredLemmas { get; init; } = [];
    public IReadOnlyList<string> RiskFactors { get; init; } = [];
    public double DiscoveryConfidence { get; init; }
}

// MATH_SOLUTION_DERIVATION — proof dependency graph + obligations + a proposed canonical answer.
public sealed record MathSolutionDerivationProposal
{
    public IReadOnlyList<MathProofNodeProposal> Nodes { get; init; } = [];
    public IReadOnlyList<MathProofEdgeProposal> Edges { get; init; } = [];
    public IReadOnlyList<MathProofObligationProposal> Obligations { get; init; } = [];
    // Explicit claims the solution makes, each linked to the obligations/theorems it depends on. Used by
    // the deterministic certainty-ceiling and prior-art gates; the LLM only PROPOSES the linkage.
    public IReadOnlyList<MathClaimProposal> Claims { get; init; } = [];
    public MathAnswerProposal? Answer { get; init; }
}

public sealed record MathClaimProposal
{
    public string? Id { get; init; }
    public string? Statement { get; init; }
    // SPECULATIVE | HYPOTHESIS | STRONGLY_SUGGESTED | CONDITIONAL | VERIFIED | EXCLUDED
    public string? AssertedStrength { get; init; }
    public IReadOnlyList<string> EssentialObligationIds { get; init; } = [];
    public IReadOnlyList<string> SupportingObligationIds { get; init; } = [];
    public IReadOnlyList<string> CitedTheoremIds { get; init; } = [];
}

public sealed record MathProofNodeProposal
{
    public string? Id { get; init; }
    public string? NodeType { get; init; }
    public string? Statement { get; init; }
    public string? Justification { get; init; }
}

public sealed record MathProofEdgeProposal
{
    public string? FromNodeId { get; init; }
    public string? ToNodeId { get; init; }
    public string? Relation { get; init; }
}

public sealed record MathProofObligationProposal
{
    public string? Id { get; init; }
    public string? ParentProofNodeId { get; init; }
    public string? Statement { get; init; }
    public string? VerificationMethod { get; init; }
    // Explicit, machine-checkable relation for deterministic verification.
    public string? NormalizedClaim { get; init; }
    public string? ExpectedRelation { get; init; }
    public double? Left { get; init; }
    public double? Right { get; init; }
    public IReadOnlyList<double> TestInputs { get; init; } = [];
    public double DiscoveryConfidence { get; init; }
    // Whether this obligation is essential to the claims that depend on it (gates the certainty ceiling).
    public bool IsEssential { get; init; } = true;
}

// MATH_STEP_VERIFICATION — the LLM's classification/preparation of obligations (never final acceptance).
public sealed record MathStepVerificationProposal
{
    public IReadOnlyList<MathStepVerificationItem> Items { get; init; } = [];
}

public sealed record MathStepVerificationItem
{
    public string? ObligationId { get; init; }
    public bool Checkable { get; init; }
    public string? NormalizedClaim { get; init; }
    public IReadOnlyList<double> TestInputs { get; init; } = [];
    public string? ExpectedRelation { get; init; }
}

// MATH_COUNTEREXAMPLE_SEARCH — refutation candidates with machine-checkable witnesses.
public sealed record MathCounterexampleSearchProposal
{
    public IReadOnlyList<MathCounterexampleCandidate> CandidateCounterexamples { get; init; } = [];
    public MathRefutationAssessment? SearchAssessment { get; init; }
}

public sealed record MathCounterexampleCandidate
{
    public string? Description { get; init; }
    public string? ConstructionMethod { get; init; }
    public string? Region { get; init; }
    public string? ViolatedPredicate { get; init; }
    public IReadOnlyList<double> WitnessValues { get; init; } = [];
}

public sealed record MathRefutationAssessment
{
    public string? CoverageNotes { get; init; }
    public double RefutationConfidence { get; init; }
}

// MATH_ANSWER_EXTRACTION — the single final answer in canonical, comparable form.
public sealed record MathAnswerProposal
{
    public string? AnswerValue { get; init; }
    public string? AnswerType { get; init; }
    public string? CanonicalForm { get; init; }
    public IReadOnlyList<string> SupportingNodeIds { get; init; } = [];
}

// MATH_SELF_CONSISTENCY — clustering of independently derived answers.
public sealed record MathSelfConsistencyProposal
{
    public IReadOnlyList<MathAnswerCluster> Clusters { get; init; } = [];
    public string? PluralityCanonicalForm { get; init; }
    public double AgreementRatio { get; init; }
    public string? DissentNotes { get; init; }
}

public sealed record MathAnswerCluster
{
    public string? CanonicalForm { get; init; }
    public int MemberCount { get; init; }
    public IReadOnlyList<string> StrategyNames { get; init; } = [];
}

// MATH_ANSWER_COMPOSER — the final, honest, user-facing result.
public sealed record MathAnswerComposerProposal
{
    public string? Outcome { get; init; }
    public string? FinalAnswer { get; init; }
    public string? SolutionSummary { get; init; }
    public IReadOnlyList<string> KeySteps { get; init; } = [];
    public string? RemainingUncertainty { get; init; }
    public string? DecisiveCheck { get; init; }
}

// ── JSON output schemas (one per MATH_* stage) ─────────────────────────────────────────────────────
// Strict object schemas passed to IAiProviderRouter.GenerateAsync as outputSchemaJson. Every schema
// sets additionalProperties=false and lists all properties in required, matching the Wide convention.
// Enum strings are SCREAMING_SNAKE and mirror the domain enums (ScientificProblemType, ProofNodeType,
// ObligationVerificationMethod, ExpectedRelation, ResolutionObjectType, ScientificOutcome, …).
public static class MathContractSchemas
{
    public const string ProblemContractSchema = """
{
  "type": "object",
  "properties": {
    "statement": { "type": ["string", "null"] },
    "problemType": { "type": ["string", "null"], "enum": ["PROVE", "DISPROVE", "CONJECTURE", "DERIVE", "COMPUTE", "SOLVE", "OPTIMIZE", "CLASSIFY", "CONSTRUCT", "EXISTENCE", "UNIQUENESS", "BOUND", "APPROXIMATE", "MODEL", "SIMULATE", "VERIFY", "EXPLAIN", null] },
    "domain": { "type": ["string", "null"], "enum": ["MATHEMATICS", "PHYSICS", "CHEMISTRY", "THEORETICAL_COMPUTER_SCIENCE", "STATISTICS", null] },
    "subdomain": { "type": ["string", "null"] },
    "givens": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "unknowns": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "assumptions": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "constraints": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "definitions": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "quantifiers": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "target": { "type": ["string", "null"] },
    "allowedMethods": { "type": "array", "maxItems": 20, "items": { "type": "string" } },
    "requiresExternalKnowledge": { "type": "boolean" },
    "requiresSymbolicComputation": { "type": "boolean" },
    "requiresNumericalComputation": { "type": "boolean" },
    "requiresFormalVerification": { "type": "boolean" }
  },
  "required": ["statement", "problemType", "domain", "subdomain", "givens", "unknowns", "assumptions", "constraints", "definitions", "quantifiers", "target", "allowedMethods", "requiresExternalKnowledge", "requiresSymbolicComputation", "requiresNumericalComputation", "requiresFormalVerification"],
  "additionalProperties": false
}
""";

    public const string StrategyProposalSchema = """
{
  "type": "object",
  "properties": {
    "candidates": {
      "type": "array",
      "maxItems": 6,
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "objectType": { "type": "string", "enum": ["PROOF_STRATEGY", "SOLUTION_METHOD", "LEMMA", "MODEL", "HYPOTHESIS", "COUNTEREXAMPLE", "MECHANISM"] },
          "mathObjectType": { "type": "string", "enum": ["THEOREM", "LEMMA", "CONJECTURE", "PROPERTY", "REGULARITY_CRITERION", "NECESSARY_CONDITION", "SUFFICIENT_CONDITION", "MECHANISM", "SCENARIO", "TAXONOMY", "STRUCTURAL_AXIS", "INVARIANT", "BOUND", "RECURRENCE", "DYNAMICAL_RELATION", "COUNTEREXAMPLE", "ANALOGY", "DESCRIPTOR"] },
          "approach": { "type": "string" },
          "applicabilityRationale": { "type": ["string", "null"] },
          "requiredLemmas": { "type": "array", "maxItems": 12, "items": { "type": "string" } },
          "riskFactors": { "type": "array", "maxItems": 12, "items": { "type": "string" } },
          "discoveryConfidence": { "type": "number" }
        },
        "required": ["name", "objectType", "mathObjectType", "approach", "applicabilityRationale", "requiredLemmas", "riskFactors", "discoveryConfidence"],
        "additionalProperties": false
      }
    }
  },
  "required": ["candidates"],
  "additionalProperties": false
}
""";

    public const string SolutionDerivationSchema = """
{
  "type": "object",
  "properties": {
    "nodes": {
      "type": "array",
      "maxItems": 40,
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "nodeType": { "type": "string", "enum": ["AXIOM", "DEFINITION", "ASSUMPTION", "KNOWN_THEOREM", "LEMMA", "PROPOSITION", "CASE", "DERIVATION", "CONTRADICTION", "COUNTEREXAMPLE", "CONCLUSION"] },
          "statement": { "type": "string" },
          "justification": { "type": ["string", "null"] }
        },
        "required": ["id", "nodeType", "statement", "justification"],
        "additionalProperties": false
      }
    },
    "edges": {
      "type": "array",
      "maxItems": 60,
      "items": {
        "type": "object",
        "properties": {
          "fromNodeId": { "type": "string" },
          "toNodeId": { "type": "string" },
          "relation": { "type": "string", "enum": ["IMPLIES", "REQUIRES", "SPECIALIZES"] }
        },
        "required": ["fromNodeId", "toNodeId", "relation"],
        "additionalProperties": false
      }
    },
    "obligations": {
      "type": "array",
      "maxItems": 40,
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "parentProofNodeId": { "type": ["string", "null"] },
          "statement": { "type": "string" },
          "verificationMethod": { "type": "string", "enum": ["ALGEBRAIC", "NUMERIC", "SYMBOLIC_IDENTITY", "CASE_EXHAUSTION", "COUNTEREXAMPLE_CHECK", "LOGICAL"] },
          "normalizedClaim": { "type": ["string", "null"] },
          "expectedRelation": { "type": ["string", "null"], "enum": ["NONE", "EQUAL", "LESS_THAN", "GREATER_THAN", "HOLDS_FOR_ALL", "EXISTS", null] },
          "left": { "type": ["number", "null"] },
          "right": { "type": ["number", "null"] },
          "testInputs": { "type": "array", "maxItems": 64, "items": { "type": "number" } },
          "discoveryConfidence": { "type": "number" },
          "isEssential": { "type": "boolean" }
        },
        "required": ["id", "parentProofNodeId", "statement", "verificationMethod", "normalizedClaim", "expectedRelation", "left", "right", "testInputs", "discoveryConfidence", "isEssential"],
        "additionalProperties": false
      }
    },
    "claims": {
      "type": "array",
      "maxItems": 20,
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "statement": { "type": "string" },
          "assertedStrength": { "type": "string", "enum": ["SPECULATIVE", "HYPOTHESIS", "STRONGLY_SUGGESTED", "CONDITIONAL", "VERIFIED", "EXCLUDED"] },
          "essentialObligationIds": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
          "supportingObligationIds": { "type": "array", "maxItems": 40, "items": { "type": "string" } },
          "citedTheoremIds": { "type": "array", "maxItems": 20, "items": { "type": "string" } }
        },
        "required": ["id", "statement", "assertedStrength", "essentialObligationIds", "supportingObligationIds", "citedTheoremIds"],
        "additionalProperties": false
      }
    },
    "answer": {
      "type": ["object", "null"],
      "properties": {
        "answerValue": { "type": ["string", "null"] },
        "answerType": { "type": ["string", "null"], "enum": ["NUMERIC", "EXACT_FRACTION", "EXPRESSION", "SET", "TUPLE", "BOOLEAN", "STATEMENT", null] },
        "canonicalForm": { "type": ["string", "null"] },
        "supportingNodeIds": { "type": "array", "maxItems": 40, "items": { "type": "string" } }
      },
      "required": ["answerValue", "answerType", "canonicalForm", "supportingNodeIds"],
      "additionalProperties": false
    }
  },
  "required": ["nodes", "edges", "obligations", "claims", "answer"],
  "additionalProperties": false
}
""";

    public const string StepVerificationSchema = """
{
  "type": "object",
  "properties": {
    "items": {
      "type": "array",
      "maxItems": 40,
      "items": {
        "type": "object",
        "properties": {
          "obligationId": { "type": "string" },
          "checkable": { "type": "boolean" },
          "normalizedClaim": { "type": ["string", "null"] },
          "testInputs": { "type": "array", "maxItems": 64, "items": { "type": "number" } },
          "expectedRelation": { "type": ["string", "null"], "enum": ["NONE", "EQUAL", "LESS_THAN", "GREATER_THAN", "HOLDS_FOR_ALL", "EXISTS", null] }
        },
        "required": ["obligationId", "checkable", "normalizedClaim", "testInputs", "expectedRelation"],
        "additionalProperties": false
      }
    }
  },
  "required": ["items"],
  "additionalProperties": false
}
""";

    public const string CounterexampleSearchSchema = """
{
  "type": "object",
  "properties": {
    "candidateCounterexamples": {
      "type": "array",
      "maxItems": 12,
      "items": {
        "type": "object",
        "properties": {
          "description": { "type": "string" },
          "constructionMethod": { "type": ["string", "null"] },
          "region": { "type": "string", "enum": ["SMALLEST_CASES", "DEGENERATE_CASES", "BOUNDARY_CASES", "ASYMPTOTIC_CASES", "RANDOM_SAMPLING"] },
          "violatedPredicate": { "type": ["string", "null"] },
          "witnessValues": { "type": "array", "maxItems": 64, "items": { "type": "number" } }
        },
        "required": ["description", "constructionMethod", "region", "violatedPredicate", "witnessValues"],
        "additionalProperties": false
      }
    },
    "searchAssessment": {
      "type": ["object", "null"],
      "properties": {
        "coverageNotes": { "type": ["string", "null"] },
        "refutationConfidence": { "type": "number" }
      },
      "required": ["coverageNotes", "refutationConfidence"],
      "additionalProperties": false
    }
  },
  "required": ["candidateCounterexamples", "searchAssessment"],
  "additionalProperties": false
}
""";

    public const string AnswerExtractionSchema = """
{
  "type": "object",
  "properties": {
    "answerValue": { "type": ["string", "null"] },
    "answerType": { "type": ["string", "null"], "enum": ["NUMERIC", "EXACT_FRACTION", "EXPRESSION", "SET", "TUPLE", "BOOLEAN", "STATEMENT", null] },
    "canonicalForm": { "type": ["string", "null"] },
    "supportingNodeIds": { "type": "array", "maxItems": 40, "items": { "type": "string" } }
  },
  "required": ["answerValue", "answerType", "canonicalForm", "supportingNodeIds"],
  "additionalProperties": false
}
""";

    public const string SelfConsistencySchema = """
{
  "type": "object",
  "properties": {
    "clusters": {
      "type": "array",
      "maxItems": 20,
      "items": {
        "type": "object",
        "properties": {
          "canonicalForm": { "type": "string" },
          "memberCount": { "type": "integer" },
          "strategyNames": { "type": "array", "maxItems": 20, "items": { "type": "string" } }
        },
        "required": ["canonicalForm", "memberCount", "strategyNames"],
        "additionalProperties": false
      }
    },
    "pluralityCanonicalForm": { "type": ["string", "null"] },
    "agreementRatio": { "type": "number" },
    "dissentNotes": { "type": ["string", "null"] }
  },
  "required": ["clusters", "pluralityCanonicalForm", "agreementRatio", "dissentNotes"],
  "additionalProperties": false
}
""";

    public const string AnswerComposerSchema = """
{
  "type": "object",
  "properties": {
    "outcome": { "type": "string", "enum": ["PROVEN", "DISPROVEN", "CONDITIONALLY_PROVEN", "COMPUTATIONALLY_SUPPORTED", "STRONGLY_SUGGESTED", "UNRESOLVED", "INSUFFICIENT_FORMALIZATION"] },
    "finalAnswer": { "type": ["string", "null"] },
    "solutionSummary": { "type": ["string", "null"] },
    "keySteps": { "type": "array", "maxItems": 30, "items": { "type": "string" } },
    "remainingUncertainty": { "type": ["string", "null"] },
    "decisiveCheck": { "type": ["string", "null"] }
  },
  "required": ["outcome", "finalAnswer", "solutionSummary", "keySteps", "remainingUncertainty", "decisiveCheck"],
  "additionalProperties": false
}
""";
}

// ── Stage contract registry (additive; not yet wired) ──────────────────────────────────────────────
// Maps each MATH_* prompt code to its output schema + a short stage directive, mirroring
// IntelligenceWideService.AstraPromptContracts. Consumed by MathPromptContractMigration to seed
// structured-output prompt rows, and by the (future) Math orchestrator when calling the AI router.
public static class MathPromptContracts
{
    public static IReadOnlyDictionary<string, (string OutputSchemaJson, string Instructions)> All { get; } =
        new Dictionary<string, (string, string)>
        {
            [IntelligencePromptCodes.MathProblemContract] = (MathContractSchemas.ProblemContractSchema,
                "Extract the structured problem contract only. Classify problemType and subdomain; separate givens, unknowns, assumptions, and constraints. Do not attempt to solve or invent constraints not implied by the problem."),
            [IntelligencePromptCodes.MathStrategyProposal] = (MathContractSchemas.StrategyProposalSchema,
                "Propose 3-5 distinct competing strategies with differentiated discoveryConfidence. Type each candidate precisely with mathObjectType (THEOREM/LEMMA/PROPERTY/REGULARITY_CRITERION/DESCRIPTOR/…) so a descriptor is never presented as a criterion. Include at least one refutation-oriented (counterexample) candidate so proof and refutation compete as siblings. Do not pick a winner."),
            [IntelligencePromptCodes.MathSolutionDerivation] = (MathContractSchemas.SolutionDerivationSchema,
                "Execute one strategy as an explicit proof dependency graph of small, individually checkable steps. Isolate every arithmetic/algebraic step as its own obligation with a machine-checkable relation, and mark isEssential=true for obligations a claim decisively depends on. Emit an explicit claim for every asserted conclusion (e.g. an exclusion or reduction), linking its essentialObligationIds and citedTheoremIds and stating assertedStrength honestly. Do not claim the result is proven."),
            [IntelligencePromptCodes.MathStepVerification] = (MathContractSchemas.StepVerificationSchema,
                "Classify and prepare each obligation for deterministic checking. Mark checkable=true only when reducible to an algebraic identity, numeric evaluation, finite case check, or factorization. Never assert final correctness here."),
            [IntelligencePromptCodes.MathCounterexampleSearch] = (MathContractSchemas.CounterexampleSearchSchema,
                "Search for counterexamples that would disprove the claim, with concrete machine-checkable witness values. Report coverage; absence of a counterexample never establishes truth."),
            [IntelligencePromptCodes.MathAnswerExtraction] = (MathContractSchemas.AnswerExtractionSchema,
                "Extract the single final answer in canonical, comparable form (reduced fractions, sorted sets, exact over decimal). Set answerValue to UNRESOLVED when the derivation is incomplete; never fabricate an answer."),
            [IntelligencePromptCodes.MathSelfConsistency] = (MathContractSchemas.SelfConsistencySchema,
                "Cluster the supplied candidate answers by canonical equality and report the plurality and agreement ratio. Agreement raises discovery confidence but is not verification."),
            [IntelligencePromptCodes.MathAnswerComposer] = (MathContractSchemas.AnswerComposerSchema,
                "Communicate the already-resolved result. Set outcome to match the supplied verification state; never translate a promising path into PROVEN. State remaining uncertainty explicitly when any critical obligation is unverified."),
            [IntelligencePromptCodes.MathFormalizationGate] = (FormalizationContractSchemas.ProofContractSchema,
                FormalizationContractSchemas.Directive)
        };
}
