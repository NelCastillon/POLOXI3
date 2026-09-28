using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Blueprint first end-to-end acceptance test: a demand letter and its response.
//
// This composes the REAL pure seams the way intake + decision reasoning do, without a database:
//   1. Extraction produces source passages; only spanned passages are admissible evidence (T11).
//   2. LegalPropositionBindingPolicy binds each extracted assertion to the matter's canonical
//      propositions — the response's denial is preserved as a DISPUTED contradiction, not a merge.
//   3. Unsupported demand conditions (no admitted evidence) remain unresolved / verification-required.
//   4. MatterPropositionClaimProjection + ClaimVerificationPrioritizer produce a POLOXI IV on the
//      shared VIV scale, and the next material evidence gap is the highest-IV unresolved proposition.
//   5. Duplicate-source correlation (both demand-letter terms cite the SAME letter) is measured and
//      raises residual uncertainty for the correlated proposition.
//
// It directly addresses the earlier gap between registered semantic dependencies and per-dependency
// evidence attachment: an assertion with no admitted span never establishes its proposition.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DemandLetterResponseAcceptanceTests
{
    private static readonly Guid SessionId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MatterId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly EpistemicAuthoritySettings Settings = new();
    private static readonly ClaimVerificationPrioritizer Prioritizer = new();

    // The matter's canonical propositions before the demand letter / response are processed.
    private static readonly LegalExistingProposition LiabilityProposition =
        new(Guid.NewGuid(), "The defendant is liable for the plaintiff's injuries.", LegalFactStates.Alleged);
    private static readonly LegalExistingProposition DamagesProposition =
        new(Guid.NewGuid(), "The plaintiff incurred fifty thousand dollars in medical damages.", LegalFactStates.Alleged);

    [Fact]
    public void DemandLetter_Terms_BindToCanonicalPropositions_WithSpanAdmission()
    {
        // ── 1. Extraction: the demand letter yields two term passages; both carry a traceable span. ──
        var liabilityPassageId = Guid.NewGuid();
        var damagesPassageId = Guid.NewGuid();
        var spannedPassages = new HashSet<Guid> { liabilityPassageId, damagesPassageId };

        Assert.True(LegalEvidenceAdmissionPolicy.IsAdmissible(liabilityPassageId, spannedPassages));
        Assert.Equal(LegalEvidenceStates.Proposed,
            LegalEvidenceAdmissionPolicy.ResolveEvidenceStateCode(damagesPassageId, spannedPassages));

        // ── 2. Binding: each demanded term reuses the canonical proposition it corroborates. ──
        var existing = new[] { LiabilityProposition, DamagesProposition };

        var liabilityBinding = LegalPropositionBindingPolicy.Resolve(
            "The defendant is liable for the plaintiff's injuries.", LegalFactStates.Alleged, existing);
        Assert.True(liabilityBinding.IsReuse);
        Assert.False(liabilityBinding.IsContradiction);
        Assert.Equal(LiabilityProposition.PropositionId, liabilityBinding.MatchedPropositionId);

        var damagesBinding = LegalPropositionBindingPolicy.Resolve(
            "The plaintiff incurred fifty thousand dollars in medical damages.", LegalFactStates.Alleged, existing);
        Assert.True(damagesBinding.IsReuse);
        Assert.Equal(DamagesProposition.PropositionId, damagesBinding.MatchedPropositionId);
    }

    [Fact]
    public void Response_Denial_IsPreservedAsDisputedContradiction()
    {
        var existing = new[] { LiabilityProposition };

        // The response letter denies liability — a contradiction reuse, never a silent merge/overwrite.
        var denialBinding = LegalPropositionBindingPolicy.Resolve(
            "The defendant did not breach any duty and is not liable for the plaintiff's injuries.",
            LegalFactStates.Disputed, existing);

        Assert.True(denialBinding.IsContradiction);
        Assert.Equal(LiabilityProposition.PropositionId, denialBinding.MatchedPropositionId);
        Assert.Equal(LegalDocumentRelationshipTypes.Contradicts, denialBinding.RelationshipTypeCode);

        // The matched proposition transitions to DISPUTED without downgrading a stronger state.
        Assert.Equal(LegalFactStates.Disputed,
            LegalPropositionBindingPolicy.ResolveReusedFactStateCode(LiabilityProposition.FactStateCode));

        // Disputed liability keeps positive POLOXI IV: it is the live question the decision turns on.
        var disputed = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(LiabilityProposition.PropositionId, MatterId,
                LiabilityProposition.PropositionText, LegalFactStates.Disputed, 0.6m,
                IsDecisionAuthoritative: true, SupportCount: 1, ContradictCount: 1),
            SessionId);

        Assert.Equal(ClaimVerificationState.Disputed, disputed.VerificationState);
        Assert.True(Prioritizer.ComputeInformationValue(disputed) >= Settings.MinimumVerificationIV);
    }

    [Fact]
    public void UnsupportedDemandCondition_RemainsUnresolved_AndIsTheNextEvidenceGap()
    {
        // A demanded condition asserted in the letter but with NO admitted supporting evidence.
        var unsupported = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(Guid.NewGuid(), MatterId,
                "The plaintiff is entitled to punitive damages for gross negligence.",
                LegalFactStates.Alleged, Confidence: 0.2m, IsDecisionAuthoritative: true,
                SupportCount: 0, ContradictCount: 0),
            SessionId);

        // Established liability (fully corroborated) — nothing left to learn, IV must be 0.
        var resolved = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(Guid.NewGuid(), MatterId,
                "The parties agree the contract was signed on the stated date.",
                LegalFactStates.Established, Confidence: 0.95m, IsDecisionAuthoritative: true,
                SupportCount: 2, ContradictCount: 0),
            SessionId);

        Assert.Equal(ClaimVerificationState.VerificationRequired, unsupported.VerificationState);
        Assert.True(unsupported.IsEssential);
        Assert.Equal(0m, Prioritizer.ComputeInformationValue(resolved));

        // The next material evidence gap is the unresolved essential condition, not the settled fact.
        var actions = Prioritizer.Prioritize(new[] { unsupported, resolved }, Settings);
        Assert.Single(actions);
        Assert.Equal(unsupported.ClaimId, actions[0].ClaimId);
    }

    [Fact]
    public void EndToEnd_DemandAndResponse_ProduceScoredGraph_WithDuplicateSourceRedundancy()
    {
        // Both demand-letter terms are extracted from the SAME letter document (one source, echoed).
        var demandLetterDocument = Guid.NewGuid();
        var liabilityEvidence = Guid.NewGuid();
        var damagesEvidence = Guid.NewGuid();

        var liabilityId = Guid.NewGuid();
        var liabilityProposition = new LegalEvidenceGraphPropositionDto(
            liabilityId, MatterId, "The defendant is liable for the plaintiff's injuries.",
            LegalFactStates.Alleged, "DYNAMIC_LLM", 0.6m, IsDecisionAuthoritative: true,
            new[]
            {
                new LegalPropositionSupportDto(Guid.NewGuid(), liabilityId, liabilityEvidence, LegalDocumentRelationshipTypes.Supports, null),
                new LegalPropositionSupportDto(Guid.NewGuid(), liabilityId, damagesEvidence, LegalDocumentRelationshipTypes.Supports, null),
            });

        // Two SUPPORTS edges, both tracing to the single demand letter → correlated corroboration.
        var sameSourceMap = new Dictionary<Guid, Guid>
        {
            [liabilityEvidence] = demandLetterDocument,
            [damagesEvidence] = demandLetterDocument,
        };
        // Same edges but from two distinct documents (letter + medical bill) → independent corroboration.
        var independentMap = new Dictionary<Guid, Guid>
        {
            [liabilityEvidence] = demandLetterDocument,
            [damagesEvidence] = Guid.NewGuid(),
        };

        var correlated = MatterPropositionClaimProjection.Project(
            MatterPropositionClaimProjection.ToSignalInput(liabilityProposition, sameSourceMap), SessionId);
        var independent = MatterPropositionClaimProjection.Project(
            MatterPropositionClaimProjection.ToSignalInput(liabilityProposition, independentMap), SessionId);

        // Duplicate-source detection: single-source support leaves more unknown than two-source support.
        Assert.True(correlated.Uncertainty > independent.Uncertainty);

        // Both remain scored on POLOXI's shared VIV scale; the decision engine is never bypassed.
        Assert.True(Prioritizer.ComputeInformationValue(correlated) > 0m);
        Assert.True(Prioritizer.ComputeInformationValue(independent) > 0m);
    }
}
