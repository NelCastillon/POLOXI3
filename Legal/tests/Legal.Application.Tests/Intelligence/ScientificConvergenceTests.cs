using Legal.Application.Features.Intelligence.Science;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Convergence policy + verification-weighted competition tests ────────────────────────────────────
// Pins the central honesty invariant: a merely plausible (high discovery confidence) proof path can
// never be classified PROVEN, and a verified counterexample always wins and disproves the conjecture.
public sealed class ScientificConvergenceTests
{
    private static ScientificProblemContract Contract() => new()
    {
        Statement = "P holds",
        ProblemType = ScientificProblemType.Prove,
        Target = "P",
    };

    private static ProofNode Node(string id, double criticality) => new()
    {
        Id = id,
        NodeType = ProofNodeType.Lemma,
        Statement = id,
        ProofCriticality = criticality,
    };

    private static ProofObligation Obligation(string parent, ObligationStatus status,
        ObligationVerificationMethod method = ObligationVerificationMethod.Algebraic) => new()
    {
        ObligationId = parent + "-o",
        ParentProofNodeId = parent,
        Statement = "step",
        VerificationMethod = method,
        Status = status,
    };

    [Fact]
    public void Evaluate_EmptyState_IsInsufficientFormalization()
    {
        var policy = new ScientificConvergencePolicy();
        var state = new ProofDerivationState { Contract = Contract() };
        Assert.Equal(ScientificOutcome.InsufficientFormalization, policy.Evaluate(state));
    }

    [Fact]
    public void Evaluate_HighDiscoveryButUnverified_IsNotProven()
    {
        var policy = new ScientificConvergencePolicy();
        var state = new ProofDerivationState
        {
            Contract = Contract(),
            Nodes = [Node("L1", 1.0)],
            Obligations = [Obligation("L1", ObligationStatus.Open)],
            DiscoveryConfidence = 0.99,
            VerificationStatus = VerificationStatus.Unverified,
        };

        Assert.NotEqual(ScientificOutcome.Proven, policy.Evaluate(state));
    }

    [Fact]
    public void Evaluate_AllCriticalVerified_IsProven()
    {
        var policy = new ScientificConvergencePolicy();
        var state = new ProofDerivationState
        {
            Contract = Contract(),
            Nodes = [Node("L1", 1.0)],
            Obligations = [Obligation("L1", ObligationStatus.Verified)],
            VerificationStatus = VerificationStatus.Verified,
        };

        Assert.Equal(ScientificOutcome.Proven, policy.Evaluate(state));
    }

    [Fact]
    public void Evaluate_RefutedObligation_IsDisproven()
    {
        var policy = new ScientificConvergencePolicy();
        var refuted = Obligation("L1", ObligationStatus.Refuted) with
        {
            CounterexampleStatus = CounterexampleStatus.Found,
        };
        var state = new ProofDerivationState
        {
            Contract = Contract(),
            Nodes = [Node("L1", 1.0)],
            Obligations = [refuted],
        };

        Assert.Equal(ScientificOutcome.Disproven, policy.Evaluate(state));
    }

    [Fact]
    public void Evaluate_CriticalVerifiedWithConditionalLemma_IsConditionallyProven()
    {
        var policy = new ScientificConvergencePolicy();
        var state = new ProofDerivationState
        {
            Contract = Contract(),
            Nodes = [Node("L1", 1.0)],
            Obligations =
            [
                Obligation("L1", ObligationStatus.Verified),
                Obligation("L1", ObligationStatus.Conditional),
            ],
        };

        Assert.Equal(ScientificOutcome.ConditionallyProven, policy.Evaluate(state));
    }

    [Fact]
    public void Evaluate_NumericChecksOnly_IsComputationallySupported()
    {
        var policy = new ScientificConvergencePolicy();
        var state = new ProofDerivationState
        {
            Contract = Contract(),
            Nodes = [Node("L1", 1.0)],
            Obligations = [Obligation("L1", ObligationStatus.Verified, ObligationVerificationMethod.Numeric)],
            VerificationStatus = VerificationStatus.Conditional,
        };

        Assert.Equal(ScientificOutcome.ComputationallySupported, policy.Evaluate(state));
    }

    [Fact]
    public void Evaluate_OnlySelfConsistency_IsStronglySuggested()
    {
        var policy = new ScientificConvergencePolicy();
        var state = new ProofDerivationState
        {
            Contract = Contract(),
            Nodes = [Node("L1", 0.2)],
            Obligations = [Obligation("L1", ObligationStatus.Open, ObligationVerificationMethod.Logical)],
            CanonicalAnswer = "42",
        };

        Assert.Equal(ScientificOutcome.StronglySuggested, policy.Evaluate(state, selfConsistencyAgreement: 0.8));
    }
}

public sealed class VerificationWeightedCompetitionTests
{
    private static ScientificCandidate Candidate(string name, ResolutionObjectType type,
        double discovery, VerificationStatus status) => new()
    {
        Id = name,
        ObjectType = type,
        Name = name,
        DiscoveryConfidence = discovery,
        VerificationStatus = status,
    };

    [Fact]
    public void VerifiedCandidate_BeatsHigherDiscoveryUnverified()
    {
        var competition = new VerificationWeightedCompetition();
        var candidates = new[]
        {
            Candidate("plausible", ResolutionObjectType.ProofStrategy, 0.99, VerificationStatus.Unverified),
            Candidate("verified", ResolutionObjectType.ProofStrategy, 0.30, VerificationStatus.Verified),
        };

        Assert.Equal("verified", competition.Leader(candidates)!.Name);
    }

    [Fact]
    public void NextToInvestigate_PicksHighestDiscoveryUnverified()
    {
        var competition = new VerificationWeightedCompetition();
        var candidates = new[]
        {
            Candidate("verified", ResolutionObjectType.ProofStrategy, 0.30, VerificationStatus.Verified),
            Candidate("promising", ResolutionObjectType.Counterexample, 0.80, VerificationStatus.Unverified),
            Candidate("weak", ResolutionObjectType.Lemma, 0.20, VerificationStatus.Unverified),
        };

        Assert.Equal("promising", competition.NextToInvestigate(candidates)!.Name);
    }

    [Fact]
    public void RefutedCandidate_SinksToBottom()
    {
        var competition = new VerificationWeightedCompetition();
        var candidates = new[]
        {
            Candidate("refuted", ResolutionObjectType.ProofStrategy, 0.99, VerificationStatus.Refuted),
            Candidate("ok", ResolutionObjectType.ProofStrategy, 0.10, VerificationStatus.Unverified),
        };

        Assert.Equal("ok", competition.Leader(candidates)!.Name);
    }
}
