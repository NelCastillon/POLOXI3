namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 claim verification state machine (§7, §8).
//
// Transitions are controlled so epistemic state changes are always explainable and auditable. The
// deterministic classifier encodes the fundamental rule:
//
//     AbsenceOfSupport ≠ EvidenceOfFalsity   →   no support does NOT mean Contradicted.
//
// There is deliberately NO code path that sets Contradicted merely because evidence is empty.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class ClaimStateMachine
{
    // Allowed forward + revision transitions. New evidence can legitimately move a claim between
    // Supported / Contradicted / Disputed / Unverified after it was first resolved.
    private static readonly IReadOnlyDictionary<ClaimVerificationState, IReadOnlySet<ClaimVerificationState>> Allowed =
        new Dictionary<ClaimVerificationState, IReadOnlySet<ClaimVerificationState>>
        {
            [ClaimVerificationState.Proposed] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.VerificationRequired,
                ClaimVerificationState.Unverifiable,
            },
            [ClaimVerificationState.VerificationRequired] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.VerificationInProgress,
                ClaimVerificationState.Unverifiable,
            },
            [ClaimVerificationState.VerificationInProgress] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.Supported,
                ClaimVerificationState.Contradicted,
                ClaimVerificationState.Disputed,
                ClaimVerificationState.Unverified,
                ClaimVerificationState.Unverifiable,
            },
            // Resolved states can revise into one another as new evidence arrives.
            [ClaimVerificationState.Supported] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.Contradicted,
                ClaimVerificationState.Disputed,
                ClaimVerificationState.Unverified,
            },
            [ClaimVerificationState.Contradicted] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.Supported,
                ClaimVerificationState.Disputed,
                ClaimVerificationState.Unverified,
            },
            [ClaimVerificationState.Disputed] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.Supported,
                ClaimVerificationState.Contradicted,
                ClaimVerificationState.Unverified,
            },
            [ClaimVerificationState.Unverified] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.VerificationRequired,
                ClaimVerificationState.VerificationInProgress,
                ClaimVerificationState.Supported,
                ClaimVerificationState.Contradicted,
                ClaimVerificationState.Disputed,
            },
            // Terminal for the current evidence set; may reopen for verification if new routes appear.
            [ClaimVerificationState.Unverifiable] = new HashSet<ClaimVerificationState>
            {
                ClaimVerificationState.VerificationRequired,
                ClaimVerificationState.VerificationInProgress,
            },
        };

    public static bool CanTransition(ClaimVerificationState from, ClaimVerificationState to)
    {
        if (from == to)
            return true;
        return Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    // Deterministically classifies a verification outcome from the observed support. This is the ONLY
    // place that maps evidence to state, and it never treats absence of support as falsity.
    public static ClaimVerificationState Classify(
        decimal supportStrength,
        decimal contradictionStrength,
        decimal adequacyThreshold)
    {
        var adequateSupport = supportStrength >= adequacyThreshold;
        var adequateContradiction = contradictionStrength >= adequacyThreshold;

        if (adequateSupport && adequateContradiction)
            return ClaimVerificationState.Disputed;
        if (adequateSupport)
            return ClaimVerificationState.Supported;
        if (adequateContradiction)
            return ClaimVerificationState.Contradicted;

        // No adequate support AND no adequate contradiction → Unverified (never Contradicted).
        return ClaimVerificationState.Unverified;
    }
}
