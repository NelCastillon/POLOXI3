using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Duplicate-source / evidence-correlation control. Verifies LegalEvidenceRedundancyPolicy measures how
// far a proposition's supporting evidence collapses onto few distinct source documents, and that the
// resulting penalty flows through the EXISTING proposition-IV projection: correlated support raises
// residual uncertainty, dampens verification strength, and never lowers information value below the
// independent-corroboration baseline.
public sealed class LegalEvidenceRedundancyPolicyTests
{
    private static readonly Guid SessionId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MatterId = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void SingleSupportingEdge_HasNoRedundancy()
    {
        var doc = Guid.NewGuid();
        Assert.Equal(0m, LegalEvidenceRedundancyPolicy.ComputeRedundancyPenalty(new Guid?[] { doc }));
    }

    [Fact]
    public void IndependentSources_HaveNoRedundancy()
    {
        var sources = new Guid?[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Assert.Equal(0m, LegalEvidenceRedundancyPolicy.ComputeRedundancyPenalty(sources));
    }

    [Fact]
    public void AllFromOneDocument_IsMaximallyCorrelated()
    {
        var doc = Guid.NewGuid();
        // 4 edges, 1 distinct source → 1 - 1/4 = 0.75.
        var penalty = LegalEvidenceRedundancyPolicy.ComputeRedundancyPenalty(new Guid?[] { doc, doc, doc, doc });
        Assert.Equal(0.75m, penalty);
    }

    [Fact]
    public void UntracedSources_AreIgnored()
    {
        var doc = Guid.NewGuid();
        // Two traced edges from the same document; two untraced (null) edges ignored → 1 - 1/2 = 0.5.
        var penalty = LegalEvidenceRedundancyPolicy.ComputeRedundancyPenalty(new Guid?[] { doc, doc, null, null });
        Assert.Equal(0.5m, penalty);
    }

    [Fact]
    public void CorrelatedSupport_RaisesUncertainty_AndReducesVerificationStrength()
    {
        var propositionId = Guid.NewGuid();
        var sharedDocument = Guid.NewGuid();
        var evidenceA = Guid.NewGuid();
        var evidenceB = Guid.NewGuid();

        // Two SUPPORTS edges that both trace to the SAME source document (correlated corroboration).
        var proposition = new LegalEvidenceGraphPropositionDto(
            propositionId, MatterId, "The defendant breached the duty of care.", LegalFactStates.Alleged, "DYNAMIC_LLM", 0.6m, true,
            new[]
            {
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, evidenceA, LegalDocumentRelationshipTypes.Supports, null),
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, evidenceB, LegalDocumentRelationshipTypes.Supports, null),
            });

        var sourceMap = new Dictionary<Guid, Guid>
        {
            [evidenceA] = sharedDocument,
            [evidenceB] = sharedDocument,
        };

        var independentSignal = MatterPropositionClaimProjection.ToSignalInput(proposition, evidenceDocumentSource: null);
        var correlatedSignal = MatterPropositionClaimProjection.ToSignalInput(proposition, sourceMap);

        Assert.Equal(0m, independentSignal.RedundancyPenalty);
        Assert.Equal(0.5m, correlatedSignal.RedundancyPenalty);

        var independent = MatterPropositionClaimProjection.Project(independentSignal, SessionId);
        var correlated = MatterPropositionClaimProjection.Project(correlatedSignal, SessionId);

        // Correlated support leaves more unknown and proves less than independent corroboration.
        Assert.True(correlated.Uncertainty > independent.Uncertainty);
        Assert.True(correlated.VerificationStrength <= independent.VerificationStrength);
    }

    [Fact]
    public void IndependentCorroboration_KeepsBaselineScoring()
    {
        var propositionId = Guid.NewGuid();
        var evidenceA = Guid.NewGuid();
        var evidenceB = Guid.NewGuid();

        var proposition = new LegalEvidenceGraphPropositionDto(
            propositionId, MatterId, "The plaintiff incurred medical expenses.", LegalFactStates.Alleged, "DYNAMIC_LLM", 0.6m, true,
            new[]
            {
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, evidenceA, LegalDocumentRelationshipTypes.Supports, null),
                new LegalPropositionSupportDto(Guid.NewGuid(), propositionId, evidenceB, LegalDocumentRelationshipTypes.Supports, null),
            });

        // Two SUPPORTS edges tracing to two DISTINCT documents → independent corroboration, no penalty.
        var sourceMap = new Dictionary<Guid, Guid>
        {
            [evidenceA] = Guid.NewGuid(),
            [evidenceB] = Guid.NewGuid(),
        };

        var signal = MatterPropositionClaimProjection.ToSignalInput(proposition, sourceMap);
        Assert.Equal(0m, signal.RedundancyPenalty);
    }
}
