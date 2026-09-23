using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA→Decision signal mapper (§12, §13).
//
// The clean integration point between epistemic governance and the existing Candidate × Branch
// engine. It converts governed claim authority (the ClaimAuthorityGate output over each claim) into
// domain-neutral DecisionBranchSignal deltas that DecisionRecompetition already consumes.
//
// Design invariants (mirror the EA core invariant so the boundary cannot leak):
//   • Authority(c)=NONE / unverified state ⇒ NO positive support signal is emitted for that claim.
//     A claim with zero AllowedPositiveContribution simply produces no positive delta — POLOXI never
//     manufactures authority from model confidence.
//   • Contradicted/Disputed claims MAY emit a NEGATIVE support delta (AllowedNegativeContribution),
//     because contradiction is meaningful evidence, not erased.
//   • The mapper is a pure projection: NO scoring happens here. Candidate × Branch recompetition
//     remains the sole authoritative scorer. The mapper only supplies signed deltas keyed by the
//     claim's lineage (SourceBranchId / SourceCandidateId).
//   • A claim with no branch/candidate lineage produces no signal (it cannot silently move the
//     ranking without a real dependency link).
//   • Deterministic: the same claims + authority decisions always yield the same signal set.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// One claim paired with the authority decision POLOXI computed for it. This is the mapper input so
// the mapper never re-evaluates the gate itself (single source of authority truth).
public sealed record GovernedClaim(ClaimProposition Claim, ClaimAuthorityDecision Authority);

public interface IEpistemicDecisionSignalMapper
{
    // Projects governed claims into the domain-neutral branch signals consumed by
    // DecisionRecompetition. Only authorized (positive) and contradicted/disputed (negative) claims
    // with real branch/candidate lineage produce signals.
    IReadOnlyList<DecisionBranchSignal> Map(IReadOnlyList<GovernedClaim> governedClaims);
}

public sealed class EpistemicDecisionSignalMapper : IEpistemicDecisionSignalMapper
{
    public IReadOnlyList<DecisionBranchSignal> Map(IReadOnlyList<GovernedClaim> governedClaims)
    {
        ArgumentNullException.ThrowIfNull(governedClaims);

        var signals = new List<DecisionBranchSignal>();

        foreach (var governed in governedClaims)
        {
            var claim = governed.Claim;
            var authority = governed.Authority;
            if (claim is null || authority is null)
                continue;

            // Net signed delta the claim is permitted to contribute. Positive support only flows for
            // authorized claims (AllowedPositiveContribution > 0); contradiction contributes negative.
            var delta = (double)(authority.AllowedPositiveContribution - authority.AllowedNegativeContribution);

            // A claim allowed to contribute nothing (unverified/unsupported) emits no signal — the
            // core invariant, expressed as the absence of a positive delta.
            if (Math.Abs(delta) < 1e-9)
                continue;

            var branchIds = ResolveBranchIds(claim);
            var candidateIds = ResolveCandidateIds(claim);

            // No lineage → no signal. A claim cannot move the ranking without a real dependency link.
            if (branchIds.Count == 0 && candidateIds.Count == 0)
                continue;

            var reasonCode = delta >= 0
                ? DecisionBranchSignalKinds.SupportChanged
                : DecisionBranchSignalKinds.ConstraintChanged;

            foreach (var branchId in branchIds)
            {
                signals.Add(new DecisionBranchSignal(
                    SignalKind: DecisionBranchSignalKinds.SupportChanged,
                    BranchId: branchId,
                    CandidateId: null,
                    SupportDelta: delta,
                    ReopenRequested: false,
                    ReasonCode: reasonCode));
            }

            // Direct candidate lineage (a claim bound straight to a candidate, no branch) also emits a
            // candidate-keyed signal so recompetition folds it into the owning candidate.
            foreach (var candidateId in candidateIds)
            {
                signals.Add(new DecisionBranchSignal(
                    SignalKind: DecisionBranchSignalKinds.SupportChanged,
                    BranchId: null,
                    CandidateId: candidateId,
                    SupportDelta: delta,
                    ReopenRequested: false,
                    ReasonCode: reasonCode));
            }
        }

        return signals;
    }

    private static IReadOnlyList<Guid> ResolveBranchIds(ClaimProposition claim)
    {
        if (claim.BranchIds.Count > 0)
            return claim.BranchIds;
        return claim.SourceBranchId is { } sourceBranch ? [sourceBranch] : [];
    }

    private static IReadOnlyList<Guid> ResolveCandidateIds(ClaimProposition claim)
    {
        // Prefer explicit candidate lineage only when there is no branch lineage: a branch-bound claim
        // is already folded into its owning candidate by branch-code prefix inside recompetition, so
        // emitting both would double-count the same support.
        if (claim.BranchIds.Count > 0 || claim.SourceBranchId is not null)
            return [];
        if (claim.CandidateIds.Count > 0)
            return claim.CandidateIds;
        return claim.SourceCandidateId is { } sourceCandidate ? [sourceCandidate] : [];
    }
}
