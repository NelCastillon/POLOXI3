using Legal.Application.Features.Intelligence.Science;

namespace Legal.Application.Features.Intelligence.Science;

// ── POLOXI Math V1 — proposal → domain mapping (runnable slice) ─────────────────────────────────────
// Converts the tolerant LLM-shaped Math*Proposal DTOs (MathContracts.cs) into the strongly-typed
// domain records the deterministic engine consumes (ScientificProblemContract, ProofNode/Edge,
// ProofObligation, ScientificCandidate, CandidateAnswer). Enum strings are parsed defensively: unknown
// or null values fall back to safe defaults rather than throwing, so a malformed LLM value never aborts
// a run — it simply produces an OPEN/unverified state the deterministic layer handles honestly.
public static class MathContractMapper
{
    public static ScientificProblemContract ToContract(MathProblemContractProposal proposal, string fallbackStatement)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var statement = FirstNonEmpty(proposal.Statement, fallbackStatement);
        return new ScientificProblemContract
        {
            Statement = statement,
            ProblemType = ParseProblemType(proposal.ProblemType),
            Domain = ParseDomain(proposal.Domain),
            Subdomain = NullIfEmpty(proposal.Subdomain),
            Givens = proposal.Givens,
            Unknowns = proposal.Unknowns,
            Assumptions = proposal.Assumptions,
            Constraints = proposal.Constraints,
            Definitions = proposal.Definitions,
            Quantifiers = proposal.Quantifiers,
            Target = FirstNonEmpty(proposal.Target, statement),
            AllowedMethods = proposal.AllowedMethods,
            RequiresExternalKnowledge = proposal.RequiresExternalKnowledge,
            RequiresSymbolicComputation = proposal.RequiresSymbolicComputation,
            RequiresNumericalComputation = proposal.RequiresNumericalComputation,
            RequiresFormalVerification = proposal.RequiresFormalVerification,
        };
    }

    public static IReadOnlyList<ProofNode> ToNodes(MathSolutionDerivationProposal derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        return derivation.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .Select(n => new ProofNode
            {
                Id = n.Id!,
                NodeType = ParseNodeType(n.NodeType),
                Statement = n.Statement ?? string.Empty,
                Justification = NullIfEmpty(n.Justification),
            })
            .ToArray();
    }

    public static IReadOnlyList<ProofEdge> ToEdges(MathSolutionDerivationProposal derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        return derivation.Edges
            .Where(e => !string.IsNullOrWhiteSpace(e.FromNodeId) && !string.IsNullOrWhiteSpace(e.ToNodeId))
            .Select(e => new ProofEdge
            {
                FromNodeId = e.FromNodeId!,
                ToNodeId = e.ToNodeId!,
                Relation = ParseEdgeRelation(e.Relation),
            })
            .ToArray();
    }

    public static IReadOnlyList<ProofObligation> ToObligations(MathSolutionDerivationProposal derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        return derivation.Obligations
            .Where(o => !string.IsNullOrWhiteSpace(o.Id))
            .Select(o => new ProofObligation
            {
                ObligationId = o.Id!,
                ParentProofNodeId = NullIfEmpty(o.ParentProofNodeId),
                Statement = o.Statement ?? string.Empty,
                VerificationMethod = ParseVerificationMethod(o.VerificationMethod),
                DiscoveryConfidence = Clamp01(o.DiscoveryConfidence),
                IsEssential = o.IsEssential,
            })
            .ToArray();
    }

    // Maps the LLM claim proposals into domain MathClaim records for the certainty-ceiling / prior-art gates.
    public static IReadOnlyList<MathClaim> ToClaims(MathSolutionDerivationProposal derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        return derivation.Claims
            .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Statement))
            .Select(c => new MathClaim
            {
                ClaimId = c.Id!,
                Statement = c.Statement!,
                AssertedStrength = ParseClaimStrength(c.AssertedStrength),
                EssentialObligationIds = c.EssentialObligationIds,
                SupportingObligationIds = c.SupportingObligationIds,
                CitedTheoremIds = c.CitedTheoremIds,
            })
            .ToArray();
    }

    // Builds the deterministic verification request for one obligation from its derivation proposal.
    public static MathVerificationRequest ToVerificationRequest(MathProofObligationProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return new MathVerificationRequest
        {
            NormalizedClaim = FirstNonEmpty(proposal.NormalizedClaim, proposal.Statement, string.Empty),
            Method = ParseVerificationMethod(proposal.VerificationMethod),
            ExpectedRelation = ParseExpectedRelation(proposal.ExpectedRelation),
            TestInputs = proposal.TestInputs,
            Left = proposal.Left,
            Right = proposal.Right,
        };
    }

    public static IReadOnlyList<ScientificCandidate> ToCandidates(MathStrategyProposal strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        return strategy.Candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select((c, index) => new ScientificCandidate
            {
                Id = $"S{index + 1}",
                ObjectType = ParseObjectType(c.ObjectType),
                MathObjectType = ParseMathObjectType(c.MathObjectType ?? c.ObjectType),
                Name = c.Name!,
                Description = NullIfEmpty(c.Approach),
                DiscoveryConfidence = Clamp01(c.DiscoveryConfidence),
            })
            .ToArray();
    }

    public static IReadOnlyList<CandidateAnswer> ToCandidateAnswers(MathSelfConsistencyProposal selfConsistency)
    {
        ArgumentNullException.ThrowIfNull(selfConsistency);
        return selfConsistency.Clusters
            .Where(c => !string.IsNullOrWhiteSpace(c.CanonicalForm))
            .SelectMany(c => (c.StrategyNames.Count > 0 ? c.StrategyNames : [c.CanonicalForm!])
                .Select(strategy => new CandidateAnswer
                {
                    StrategyName = strategy,
                    CanonicalForm = c.CanonicalForm!,
                }))
            .ToArray();
    }

    public static ScientificAnswerType ParseAnswerType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "NUMERIC" => ScientificAnswerType.Numeric,
        "EXACT_FRACTION" => ScientificAnswerType.ExactFraction,
        "EXPRESSION" => ScientificAnswerType.Expression,
        "SET" => ScientificAnswerType.Set,
        "TUPLE" => ScientificAnswerType.Tuple,
        "BOOLEAN" => ScientificAnswerType.Boolean,
        "STATEMENT" => ScientificAnswerType.Statement,
        _ => ScientificAnswerType.Statement,
    };

    public static ScientificOutcome ParseOutcome(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "PROVEN" => ScientificOutcome.Proven,
        "DISPROVEN" => ScientificOutcome.Disproven,
        "CONDITIONALLY_PROVEN" => ScientificOutcome.ConditionallyProven,
        "COMPUTATIONALLY_SUPPORTED" => ScientificOutcome.ComputationallySupported,
        "STRONGLY_SUGGESTED" => ScientificOutcome.StronglySuggested,
        "INSUFFICIENT_FORMALIZATION" => ScientificOutcome.InsufficientFormalization,
        _ => ScientificOutcome.Unresolved,
    };

    private static ScientificProblemType ParseProblemType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "PROVE" => ScientificProblemType.Prove,
        "DISPROVE" => ScientificProblemType.Disprove,
        "CONJECTURE" => ScientificProblemType.Conjecture,
        "DERIVE" => ScientificProblemType.Derive,
        "COMPUTE" => ScientificProblemType.Compute,
        "SOLVE" => ScientificProblemType.Solve,
        "OPTIMIZE" => ScientificProblemType.Optimize,
        "CLASSIFY" => ScientificProblemType.Classify,
        "CONSTRUCT" => ScientificProblemType.Construct,
        "EXISTENCE" => ScientificProblemType.Existence,
        "UNIQUENESS" => ScientificProblemType.Uniqueness,
        "BOUND" => ScientificProblemType.Bound,
        "APPROXIMATE" => ScientificProblemType.Approximate,
        "MODEL" => ScientificProblemType.Model,
        "SIMULATE" => ScientificProblemType.Simulate,
        "VERIFY" => ScientificProblemType.Verify,
        "EXPLAIN" => ScientificProblemType.Explain,
        _ => ScientificProblemType.Solve,
    };

    private static ScientificDomain ParseDomain(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "PHYSICS" => ScientificDomain.Physics,
        "CHEMISTRY" => ScientificDomain.Chemistry,
        "THEORETICAL_COMPUTER_SCIENCE" => ScientificDomain.TheoreticalComputerScience,
        "STATISTICS" => ScientificDomain.Statistics,
        _ => ScientificDomain.Mathematics,
    };

    private static ProofNodeType ParseNodeType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "AXIOM" => ProofNodeType.Axiom,
        "DEFINITION" => ProofNodeType.Definition,
        "ASSUMPTION" => ProofNodeType.Assumption,
        "KNOWN_THEOREM" => ProofNodeType.KnownTheorem,
        "LEMMA" => ProofNodeType.Lemma,
        "PROPOSITION" => ProofNodeType.Proposition,
        "CASE" => ProofNodeType.Case,
        "DERIVATION" => ProofNodeType.Derivation,
        "CONTRADICTION" => ProofNodeType.Contradiction,
        "COUNTEREXAMPLE" => ProofNodeType.Counterexample,
        "CONCLUSION" => ProofNodeType.Conclusion,
        _ => ProofNodeType.Derivation,
    };

    private static ProofEdgeRelation ParseEdgeRelation(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "REQUIRES" => ProofEdgeRelation.Requires,
        "SPECIALIZES" => ProofEdgeRelation.Specializes,
        _ => ProofEdgeRelation.Implies,
    };

    private static ObligationVerificationMethod ParseVerificationMethod(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "ALGEBRAIC" => ObligationVerificationMethod.Algebraic,
        "NUMERIC" => ObligationVerificationMethod.Numeric,
        "SYMBOLIC_IDENTITY" => ObligationVerificationMethod.SymbolicIdentity,
        "CASE_EXHAUSTION" => ObligationVerificationMethod.CaseExhaustion,
        "COUNTEREXAMPLE_CHECK" => ObligationVerificationMethod.CounterexampleCheck,
        _ => ObligationVerificationMethod.Logical,
    };

    private static ExpectedRelation ParseExpectedRelation(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "EQUAL" => ExpectedRelation.Equal,
        "LESS_THAN" => ExpectedRelation.LessThan,
        "GREATER_THAN" => ExpectedRelation.GreaterThan,
        "HOLDS_FOR_ALL" => ExpectedRelation.HoldsForAll,
        "EXISTS" => ExpectedRelation.Exists,
        _ => ExpectedRelation.None,
    };

    private static ResolutionObjectType ParseObjectType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "PROOF_STRATEGY" => ResolutionObjectType.ProofStrategy,
        "SOLUTION_METHOD" => ResolutionObjectType.ProofStrategy,
        "LEMMA" => ResolutionObjectType.Lemma,
        "MODEL" => ResolutionObjectType.Model,
        "HYPOTHESIS" => ResolutionObjectType.Hypothesis,
        "COUNTEREXAMPLE" => ResolutionObjectType.Counterexample,
        "MECHANISM" => ResolutionObjectType.Mechanism,
        _ => ResolutionObjectType.ProofStrategy,
    };

    // Maps an LLM object-type string to the precise MathematicalObjectType. Falls back to Unspecified so an
    // untyped or unknown value never masquerades as a stronger object (e.g. a criterion).
    public static MathematicalObjectType ParseMathObjectType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "THEOREM" => MathematicalObjectType.Theorem,
        "LEMMA" => MathematicalObjectType.Lemma,
        "CONJECTURE" => MathematicalObjectType.Conjecture,
        "PROPERTY" => MathematicalObjectType.Property,
        "REGULARITY_CRITERION" => MathematicalObjectType.RegularityCriterion,
        "NECESSARY_CONDITION" => MathematicalObjectType.NecessaryCondition,
        "SUFFICIENT_CONDITION" => MathematicalObjectType.SufficientCondition,
        "MECHANISM" => MathematicalObjectType.Mechanism,
        "SCENARIO" => MathematicalObjectType.Scenario,
        "TAXONOMY" => MathematicalObjectType.Taxonomy,
        "STRUCTURAL_AXIS" => MathematicalObjectType.StructuralAxis,
        "INVARIANT" => MathematicalObjectType.Invariant,
        "BOUND" => MathematicalObjectType.Bound,
        "RECURRENCE" => MathematicalObjectType.Recurrence,
        "DYNAMICAL_RELATION" => MathematicalObjectType.DynamicalRelation,
        "COUNTEREXAMPLE" => MathematicalObjectType.Counterexample,
        "ANALOGY" => MathematicalObjectType.Analogy,
        "DESCRIPTOR" => MathematicalObjectType.Descriptor,
        _ => MathematicalObjectType.Unspecified,
    };

    // Maps an LLM claim-strength string to the domain ClaimStrength. Falls back to Hypothesis so an unknown
    // value can never be reported as verified/excluded.
    public static ClaimStrength ParseClaimStrength(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "SPECULATIVE" => ClaimStrength.Speculative,
        "HYPOTHESIS" => ClaimStrength.Hypothesis,
        "STRONGLY_SUGGESTED" => ClaimStrength.StronglySuggested,
        "CONDITIONAL" => ClaimStrength.Conditional,
        "VERIFIED" => ClaimStrength.Verified,
        "EXCLUDED" => ClaimStrength.Excluded,
        _ => ClaimStrength.Hypothesis,
    };

    private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}
