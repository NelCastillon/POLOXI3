using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — matter-proposition → ClaimProposition projection.
//
// Atomic matter fact-propositions (POLOXI.Legal_MatterFactProposition) are units of uncertainty,
// scoring, and Information Value. Rather than build a second scorer, we project each proposition into
// the authoritative ClaimProposition shape so the EXISTING ClaimVerificationPrioritizer can score it
// on POLOXI's single VIV scale. The four decision-relevance signals (Materiality, Uncertainty,
// DecisionImpact, Discrimination) and the verification state are derived deterministically from the
// proposition's FactStateCode, Confidence, IsDecisionAuthoritative, and its SUPPORTS/CONTRADICTS edges.
//
// Pure and side-effect-free (mirrors LegalEvidenceAdmissionPolicy / LegalPropositionBindingPolicy) so
// it is unit-testable without a database and never touches the authoritative branch-target IV loop.
// ─────────────────────────────────────────────────────────────────────────────────────────────

// A minimal, storage-neutral view of a matter fact-proposition plus its evidence-relationship counts.
// RedundancyPenalty (0..1) measures how far the supporting evidence collapses onto few distinct source
// documents — correlated corroboration that must not inflate apparent support (see
// LegalEvidenceRedundancyPolicy).
public sealed record MatterPropositionSignalInput(
    Guid PropositionId,
    Guid MatterId,
    string PropositionText,
    string FactStateCode,
    decimal? Confidence,
    bool IsDecisionAuthoritative,
    int SupportCount,
    int ContradictCount,
    decimal RedundancyPenalty = 0m);

public static class MatterPropositionClaimProjection
{
    // Neutral confidence when a proposition carries none, so an unscored proposition is neither
    // treated as certain nor as maximally uncertain.
    private const decimal DefaultConfidence = 0.5m;

    // Build the storage-neutral signal input from an evidence-graph proposition DTO.
    public static MatterPropositionSignalInput ToSignalInput(LegalEvidenceGraphPropositionDto proposition)
        => ToSignalInput(proposition, evidenceDocumentSource: null);

    // Build the signal input, additionally computing a duplicate-source redundancy penalty from the
    // originating documents of the proposition's ADMITTED supporting evidence. evidenceDocumentSource
    // maps an evidence item id to the document it came from; when null (or unresolved) redundancy is 0
    // so callers without source provenance behave exactly as before.
    public static MatterPropositionSignalInput ToSignalInput(
        LegalEvidenceGraphPropositionDto proposition,
        IReadOnlyDictionary<Guid, Guid>? evidenceDocumentSource)
    {
        ArgumentNullException.ThrowIfNull(proposition);
        var support = proposition.Support ?? [];
        var supportEdges = support
            .Where(edge => string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var supportCount = supportEdges.Length;
        var contradictCount = support.Count(edge =>
            string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase));

        // One source-document entry per supporting edge (repeat the same document id when several edges
        // cite the same source) so LegalEvidenceRedundancyPolicy can measure independent-vs-correlated
        // corroboration. Edges whose source cannot be resolved contribute a null and are ignored.
        var sourceDocumentIds = supportEdges
            .Select(edge => evidenceDocumentSource is not null
                && evidenceDocumentSource.TryGetValue(edge.LegalEvidenceItemId, out var documentId)
                    ? (Guid?)documentId
                    : null)
            .ToArray();
        var redundancy = LegalEvidenceRedundancyPolicy.ComputeRedundancyPenalty(sourceDocumentIds);

        return new MatterPropositionSignalInput(
            proposition.LegalFactPropositionId,
            proposition.MatterId,
            proposition.PropositionText,
            proposition.FactStateCode,
            proposition.Confidence,
            proposition.IsDecisionAuthoritative,
            supportCount,
            contradictCount,
            redundancy);
    }

    // Deterministically project a matter proposition into the authoritative ClaimProposition shape so
    // the existing ClaimVerificationPrioritizer can score its Information Value on the shared VIV scale.
    public static ClaimProposition Project(MatterPropositionSignalInput input, Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(input);

        var confidence = Clamp(input.Confidence ?? DefaultConfidence);
        var hasContradiction = input.ContradictCount > 0;
        var hasSupport = input.SupportCount > 0;
        var state = ResolveVerificationState(input.FactStateCode, hasContradiction);
        var redundancy = Clamp(input.RedundancyPenalty);

        // Uncertainty: what remains unknown about the proposition. Base is (1 - confidence); an open
        // dispute or an unsupported allegation floors it upward because there is more to learn.
        var uncertainty = 1m - confidence;
        if (state is ClaimVerificationState.Disputed)
            uncertainty = Math.Max(uncertainty, 0.7m);
        else if (!hasSupport && state is ClaimVerificationState.VerificationRequired)
            uncertainty = Math.Max(uncertainty, 0.6m);
        // Duplicate-source correlation: support that collapses onto few distinct documents leaves more
        // unknown than the raw confidence implies, so redundancy raises residual uncertainty toward 1.
        if (redundancy > 0m)
            uncertainty += (1m - uncertainty) * redundancy;
        uncertainty = Clamp(uncertainty);

        // Materiality / DecisionImpact: an authoritative proposition drives the decision more than a
        // non-authoritative one; a contested proposition raises the stakes of resolving it.
        var materiality = input.IsDecisionAuthoritative ? 0.8m : 0.4m;
        if (hasContradiction)
            materiality = Math.Min(1m, materiality + 0.1m);
        materiality = Clamp(materiality);

        var decisionImpact = input.IsDecisionAuthoritative ? 0.7m : 0.4m;
        if (hasContradiction)
            decisionImpact = Math.Min(1m, decisionImpact + 0.15m);
        decisionImpact = Clamp(decisionImpact);

        // Discrimination: how strongly resolving this proposition separates close outcomes. A contested
        // (support vs contradiction) proposition discriminates most; a settled one discriminates least.
        var discrimination = hasContradiction && hasSupport ? 0.8m
            : hasContradiction ? 0.6m
            : hasSupport ? 0.4m
            : 0.3m;

        // An unresolved authoritative proposition can block decision readiness even if ranking is stable.
        var isEssential = input.IsDecisionAuthoritative && NeedsVerification(state);

        var normalized = input.PropositionText?.Trim().ToUpperInvariant() ?? string.Empty;

        return new ClaimProposition
        {
            ClaimId = input.PropositionId,
            SessionId = sessionId,
            MatterId = input.MatterId,
            Text = input.PropositionText ?? string.Empty,
            NormalizedText = normalized,
            ClaimType = ClaimType.Factual,
            Origin = ClaimOrigin.SystemGenerated,
            VerificationState = state,
            DecisionAuthority = input.IsDecisionAuthoritative ? ClaimDecisionAuthority.Limited : ClaimDecisionAuthority.None,
            VerificationStrength = hasSupport && !hasContradiction ? Clamp(confidence * (1m - redundancy)) : 0m,
            Materiality = materiality,
            DecisionImpact = decisionImpact,
            Discrimination = discrimination,
            Uncertainty = uncertainty,
            IsEssential = isEssential,
            ProposedByModel = null,
            Version = 1,
        };
    }

    // Map the legal fact-state vocabulary onto the domain-neutral verification state. Verified/resolved
    // states (SUPPORTED/ESTABLISHED without contradiction) map to Supported so their IV is 0 — there is
    // nothing more to learn; contested or unsupported states remain verification-worthy.
    private static ClaimVerificationState ResolveVerificationState(string? factStateCode, bool hasContradiction)
    {
        var code = factStateCode?.Trim().ToUpperInvariant();
        return code switch
        {
            LegalFactStates.Disputed => ClaimVerificationState.Disputed,
            LegalFactStates.Invalidated => ClaimVerificationState.Contradicted,
            LegalFactStates.Established => hasContradiction ? ClaimVerificationState.Disputed : ClaimVerificationState.Supported,
            LegalFactStates.Supported => hasContradiction ? ClaimVerificationState.Disputed : ClaimVerificationState.Supported,
            LegalFactStates.Alleged => ClaimVerificationState.VerificationRequired,
            _ => ClaimVerificationState.VerificationRequired,
        };
    }

    private static bool NeedsVerification(ClaimVerificationState state) => state is
        ClaimVerificationState.Proposed or
        ClaimVerificationState.VerificationRequired or
        ClaimVerificationState.VerificationInProgress or
        ClaimVerificationState.Unverified or
        ClaimVerificationState.Disputed;

    private static decimal Clamp(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;
}
