namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — Channel → typed DecisionBranchSignal adapter (DOMAIN side of the admission boundary).
//
// This is NOT part of POLOXI Core. It lives on the Judz/Legal domain side and its ONLY job is to
// translate qualitative, verified channel contributions into the domain-neutral, POLOXI-native
// DecisionBranchSignal contract that DecisionRecompetition already consumes. It:
//   * resolves each contribution's run-scoped target node to authoritative branch/candidate lineage
//     via IChannelContributionLineageResolver (the node→branch/candidate mapping is NOT persisted on
//     the hierarchy node — migration 0366 — so lineage is supplied, never guessed here);
//   * classifies the contribution's qualitative effect (support / contradiction / reopen / context);
//   * emits TYPED signals — δ lands only on the POLOXI input dimension the contribution informs
//     (EvidenceSupport → Evidence, AuthoritySupport → Authority, …) instead of the legacy V+A+E
//     coupling — so a verified Evidence contribution moves Evidence and no unrelated dimension.
//
// STRICT INVARIANTS (mirror EpistemicDecisionSignalMapper — the boundary cannot leak):
//   • NO scoring, NO clamping, NO ranking, NO call into DecisionRecompetition/DecisionCoreMath. The
//     adapter only CONSTRUCTS DecisionBranchSignal values; POLOXI Core decides the consequence.
//   • Only Verified support contributions emit a positive δ (mirrors ContributesPositiveSupport).
//     Contradicts/Invalidates emit a negative δ; Challenges requests reopen (no positive δ).
//     ContextOnly/Insufficient/Refuted/Disputed-without-contradiction emit nothing.
//   • No lineage → no signal. A contribution cannot move the ranking without a real dependency link.
//   • Pure and deterministic: the same contributions + lineage always yield the same signal set.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public interface ILegalChannelSignalAdapter
{
    // Projects verified channel contributions into typed, domain-neutral branch signals. Contributions
    // with no lineage, no effect, or ineligible verification state simply produce no signal.
    IReadOnlyList<DecisionBranchSignal> Project(IReadOnlyList<DecisionContribution> contributions);
}

public sealed class LegalChannelSignalAdapter(IChannelContributionLineageResolver lineageResolver) : ILegalChannelSignalAdapter
{
    // Bounded, qualitative-to-signed magnitudes. These are NOT scores: they are the fixed δ a
    // qualitative relation is permitted to contribute, always within POLOXI's [-1,1] signal range.
    // POLOXI still owns whether that δ changes strength, uncertainty, margin, or the winner.
    // Constants + classification live in ChannelScoringFormula (single source of truth) so the live
    // scoring path and the read-only Channel Scoring LPI trace can never drift apart.
    private const double SupportMagnitude = ChannelScoringFormula.SupportMagnitude;
    private const double QualifiedSupportMagnitude = ChannelScoringFormula.QualifiedSupportMagnitude;
    private const double ContradictionMagnitude = ChannelScoringFormula.ContradictionMagnitude;

    private readonly IChannelContributionLineageResolver _lineageResolver =
        lineageResolver ?? throw new ArgumentNullException(nameof(lineageResolver));

    public IReadOnlyList<DecisionBranchSignal> Project(IReadOnlyList<DecisionContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var signals = new List<DecisionBranchSignal>();

        foreach (var contribution in contributions)
        {
            if (contribution is null)
                continue;

            var effect = ClassifyEffect(contribution);
            if (effect == ContributionEffect.None)
                continue;

            // No lineage → no signal. The channel boundary never moves the ranking without a link.
            var lineage = _lineageResolver.Resolve(contribution) ?? ChannelContributionLineage.None;
            if (!lineage.HasLineage)
                continue;

            var target = MapTarget(contribution.TargetSignalCode);
            var reopen = effect == ContributionEffect.Reopen;
            var delta = effect switch
            {
                ContributionEffect.Support => SupportDelta(contribution),
                ContributionEffect.Contradict => ContradictionMagnitude,
                _ => 0.0, // Reopen carries no direct δ; it requests POLOXI to reopen verification.
            };

            var reasonCode = delta >= 0
                ? DecisionBranchSignalKinds.SupportChanged
                : DecisionBranchSignalKinds.ConstraintChanged;
            if (reopen)
                reasonCode = DecisionBranchSignalKinds.ReopenRequested;

            foreach (var branchId in lineage.BranchIds)
            {
                signals.Add(new DecisionBranchSignal(
                    SignalKind: reopen ? DecisionBranchSignalKinds.ReopenRequested : DecisionBranchSignalKinds.SupportChanged,
                    BranchId: branchId,
                    CandidateId: null,
                    SupportDelta: delta,
                    ReopenRequested: reopen,
                    ReasonCode: reasonCode,
                    TargetSignal: target));
            }

            // Direct candidate lineage (a contribution bound straight to a candidate, no branch) also
            // emits a candidate-keyed signal so recompetition folds it into the owning candidate. Only
            // when there is no branch lineage, to avoid double-counting the same support.
            if (lineage.BranchIds.Count == 0)
            {
                foreach (var candidateId in lineage.CandidateIds)
                {
                    signals.Add(new DecisionBranchSignal(
                        SignalKind: reopen ? DecisionBranchSignalKinds.ReopenRequested : DecisionBranchSignalKinds.SupportChanged,
                        BranchId: null,
                        CandidateId: candidateId,
                        SupportDelta: delta,
                        ReopenRequested: reopen,
                        ReasonCode: reasonCode,
                        TargetSignal: target));
                }
            }
        }

        return signals;
    }

    // Resolves the positive support δ for a supporting contribution. The base band is the fixed
    // [QualifiedSupportMagnitude, SupportMagnitude] a qualitative relation is permitted to contribute.
    // When the channel supplied a placement Magnitude (the attorney's deliberate relative position
    // WITHIN the sibling band they chose — including a value overwritten in the UI), that [0,1]
    // intelligence linearly selects where inside the permitted band this δ lands, so a stronger
    // placement earns a stronger (but still bounded) support δ. Null Magnitude → fixed constant,
    // preserving byte-identical legacy behavior. POLOXI Core still owns the final consequence.
    private static double SupportDelta(DecisionContribution contribution)
    {
        var ceiling = contribution.Relation == ContributionRelation.Qualifies
            ? QualifiedSupportMagnitude
            : SupportMagnitude;

        if (contribution.Magnitude is not { } magnitude)
            return ceiling;

        var position = Math.Clamp(magnitude, 0.0, 1.0);
        return QualifiedSupportMagnitude + (ceiling - QualifiedSupportMagnitude) * position;
    }

    // The qualitative effect a contribution is permitted to have on POLOXI, gated by verification.
    private enum ContributionEffect
    {
        None = 0,
        Support = 1,
        Contradict = 2,
        Reopen = 3,
    }

    private static ContributionEffect ClassifyEffect(DecisionContribution contribution)
    {
        // A challenge reopens verification regardless of numeric support (Human Intelligence pattern).
        if (contribution.Relation == ContributionRelation.Challenges)
            return ContributionEffect.Reopen;

        // Contradiction/invalidation is meaningful evidence even against a verified opposing fact.
        if (contribution.Relation is ContributionRelation.Contradicts or ContributionRelation.Invalidates)
            return ContributionEffect.Contradict;

        // Positive support only flows for VERIFIED establishing/supporting/qualifying contributions —
        // POLOXI never manufactures support from unverified model output.
        if (contribution.VerificationState == ContributionVerificationState.Verified
            && contribution.Relation is ContributionRelation.Supports
                or ContributionRelation.Establishes
                or ContributionRelation.Qualifies)
        {
            return ContributionEffect.Support;
        }

        // ContextOnly / Insufficient / unverified support → no signal.
        return ContributionEffect.None;
    }

    // Maps the existing POLOXI decision-support signal vocabulary to the typed candidate input
    // dimension the δ must land on. Unknown/uncertainty codes map to a null target (legacy coupling).
    private static DecisionSignalTarget? MapTarget(string? targetSignalCode) => targetSignalCode switch
    {
        DecisionChannelCodes.TargetSignal.EvidenceSupport => DecisionSignalTarget.Evidence,
        DecisionChannelCodes.TargetSignal.FactSupport => DecisionSignalTarget.Fact,
        DecisionChannelCodes.TargetSignal.AuthoritySupport => DecisionSignalTarget.Authority,
        DecisionChannelCodes.TargetSignal.LegalSupport => DecisionSignalTarget.Legal,
        _ => null, // Uncertainty / unrecognized → legacy undifferentiated coupling.
    };
}
