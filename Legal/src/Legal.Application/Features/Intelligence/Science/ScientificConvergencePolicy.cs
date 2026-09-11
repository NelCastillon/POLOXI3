namespace Legal.Application.Features.Intelligence.Science;

// ── Scientific / mathematical convergence (additive; not yet wired) ────────────────────────────────
// Research convergence asks "is the evidence sufficient?". Mathematics needs different criteria:
//   Converge = ProblemStable ∧ ProofPathStable ∧ CriticalObligationsResolved
//              ∧ NoKnownCounterexample ∧ VerificationSufficient
// Crucially, "no known counterexample" does NOT mean "proven". The outcome enum reflects that honestly.
public enum ScientificOutcome
{
    Proven,
    Disproven,
    ConditionallyProven,
    ComputationallySupported,
    StronglySuggested,
    Unresolved,
    InsufficientFormalization,
}

// Pure, deterministic policy that classifies a ProofDerivationState into a ScientificOutcome. It never
// promotes discovery confidence into a proof: only VERIFIED obligations authorize Proven/Disproven.
public sealed class ScientificConvergencePolicy
{
    // Agreement ratio above which self-consistency alone justifies STRONGLY_SUGGESTED.
    private readonly double _strongAgreementThreshold;

    public ScientificConvergencePolicy(double strongAgreementThreshold = 0.6)
    {
        _strongAgreementThreshold = strongAgreementThreshold;
    }

    public ScientificOutcome Evaluate(ProofDerivationState state, double selfConsistencyAgreement = 0d)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Nodes.Count == 0 && state.Obligations.Count == 0)
        {
            return ScientificOutcome.InsufficientFormalization;
        }

        // A verified counterexample / refuted critical obligation disproves the claim.
        var hasRefutation = state.VerificationStatus == VerificationStatus.Refuted
            || state.Obligations.Any(o => o.Status == ObligationStatus.Refuted
                && o.CounterexampleStatus == CounterexampleStatus.Found);
        if (hasRefutation)
        {
            return ScientificOutcome.Disproven;
        }

        var criticalObligations = state.Obligations
            .Where(o => IsCritical(state, o))
            .ToArray();

        var allCriticalVerified = criticalObligations.Length > 0
            && criticalObligations.All(o => o.Status == ObligationStatus.Verified);

        var anyCriticalOpen = criticalObligations.Any(o =>
            o.Status is ObligationStatus.Open or ObligationStatus.Unresolved);

        var anyConditional = state.Obligations.Any(o => o.Status == ObligationStatus.Conditional);

        // Every critical obligation is either verified or conditional (nothing open/unresolved).
        var allCriticalResolved = criticalObligations.Length > 0 && !anyCriticalOpen;

        // Fully verified proof path with formal verification satisfied.
        if (state.VerificationStatus == VerificationStatus.Verified && allCriticalVerified && !anyConditional)
        {
            return ScientificOutcome.Proven;
        }

        // Verified except for unproven lemmas the result depends on.
        if (allCriticalResolved && anyConditional)
        {
            return ScientificOutcome.ConditionallyProven;
        }

        // Numeric / case checks passed but no formal proof.
        if (!anyCriticalOpen && criticalObligations.Length > 0
            && criticalObligations.Any(o => o.VerificationMethod is ObligationVerificationMethod.Numeric
                or ObligationVerificationMethod.CaseExhaustion))
        {
            return ScientificOutcome.ComputationallySupported;
        }

        // Only self-consistency agreement supports the answer.
        if (selfConsistencyAgreement >= _strongAgreementThreshold && state.CanonicalAnswer is not null)
        {
            return ScientificOutcome.StronglySuggested;
        }

        return ScientificOutcome.Unresolved;
    }

    // A branch that could invalidate the whole proof gets priority. Criticality >= 0.5 counts as critical;
    // if criticality is unset (all zero), every obligation is treated as critical to stay conservative.
    private static bool IsCritical(ProofDerivationState state, ProofObligation obligation)
    {
        if (obligation.ParentProofNodeId is null)
        {
            return true;
        }

        var node = state.Nodes.FirstOrDefault(n => n.Id == obligation.ParentProofNodeId);
        if (node is null)
        {
            return true;
        }

        var anyCriticalityAssigned = state.Nodes.Any(n => n.ProofCriticality > 0d);
        return !anyCriticalityAssigned || node.ProofCriticality >= 0.5d;
    }
}
