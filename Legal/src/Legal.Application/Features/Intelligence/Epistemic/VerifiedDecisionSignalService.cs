using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — deterministic projection service (frozen slice-1 design).
//
// Converts verified decision-support signals into the domain-neutral DecisionBranchSignal deltas
// the existing DecisionRecompetition engine already consumes. This service is a PURE projection:
// no scoring happens here — Candidate × Branch recompetition remains the sole authoritative scorer.
//
// The service is the single enforcement point for the core invariant:
//   • RequiresVerification ∧ state ≠ Supported ⇒ NO positive support delta (zero authority).
//   • Contradicted ⇒ negative support delta (contradiction is meaningful evidence).
//   • Disputed / Unverified (when required) ⇒ no positive delta.
//   • No branch/candidate lineage ⇒ no signal emitted at all.
//   • Deterministic: identical inputs always yield the same, order-stable signal set.
// ─────────────────────────────────────────────────────────────────────────────────────────────

public interface IVerifiedDecisionSignalService
{
    // Projects verified support signals into the domain-neutral branch signals consumed by
    // DecisionRecompetition. Only Supported (positive) and Contradicted (negative) signals with real
    // branch/candidate lineage produce output.
    IReadOnlyList<DecisionBranchSignal> Project(IReadOnlyList<DecisionSupportSignal> signals);
}

public sealed class VerifiedDecisionSignalService : IVerifiedDecisionSignalService
{
    private const double Epsilon = 1e-9;

    public IReadOnlyList<DecisionBranchSignal> Project(IReadOnlyList<DecisionSupportSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var projected = new List<DecisionBranchSignal>();

        foreach (var signal in signals)
        {
            if (signal is null)
                continue;

            var delta = AllowedDelta(signal);
            if (Math.Abs(delta) < Epsilon)
                continue;

            // No lineage → no signal. A support statement cannot move the ranking without a real
            // dependency link to an authoritative branch or candidate.
            var hasBranch = signal.SourceBranchId is not null;
            var hasCandidate = signal.SourceCandidateId is not null;
            if (!hasBranch && !hasCandidate)
                continue;

            var reasonCode = delta >= 0
                ? DecisionBranchSignalKinds.SupportChanged
                : DecisionBranchSignalKinds.ConstraintChanged;

            if (hasBranch)
            {
                projected.Add(new DecisionBranchSignal(
                    SignalKind: DecisionBranchSignalKinds.SupportChanged,
                    BranchId: signal.SourceBranchId,
                    CandidateId: null,
                    SupportDelta: delta,
                    ReopenRequested: false,
                    ReasonCode: reasonCode));
            }

            if (hasCandidate)
            {
                projected.Add(new DecisionBranchSignal(
                    SignalKind: DecisionBranchSignalKinds.SupportChanged,
                    BranchId: null,
                    CandidateId: signal.SourceCandidateId,
                    SupportDelta: delta,
                    ReopenRequested: false,
                    ReasonCode: reasonCode));
            }
        }

        return projected;
    }

    // The signed delta a signal is permitted to contribute, enforcing the core invariant. The
    // magnitude is scaled by DecisionImpact (and VerificationStrength for positive support) so that
    // weakly-verified or low-impact signals move the ranking less.
    private static double AllowedDelta(DecisionSupportSignal signal)
    {
        var impact = Clamp01((double)signal.DecisionImpact);
        if (impact < Epsilon)
            return 0d;

        // Contradiction is meaningful evidence → negative delta regardless of RequiresVerification.
        if (signal.VerificationState == DecisionSupportVerificationState.Contradicted)
            return -impact;

        if (signal.VerificationState == DecisionSupportVerificationState.Supported)
        {
            // Positive support flows for supported signals. When verification is required, the
            // strength gates how much positive authority is granted.
            var strength = signal.RequiresVerification
                ? Clamp01((double)signal.VerificationStrength)
                : 1d;
            return impact * strength;
        }

        // Unverified / Disputed signals: the core invariant blocks any positive contribution ONLY
        // when verification is required (material signals). A signal that does not require
        // verification (immaterial) may contribute at full impact.
        return signal.RequiresVerification ? 0d : impact;
    }

    private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
}
