using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI v4.0 Proposition Integrity Bridge control. Verifies the advisory wiring seam maps persisted
// evidence-graph rows into the pure pillars and runs the §9 gate: an admitted, span-backed support yields
// PASS; an unsupported proposition stays PASS_WITH_UNRESOLVED (never blocked); a disputed proposition maps
// to the APR open-challenge path; support edges collapsing onto one document are reported correlated-only.
public sealed class LegalPropositionIntegrityBridgeTests
{
    private static LegalEvidenceGraphItemDto Evidence(Guid id, Guid docId, bool withSpan)
        => new(
            id, Guid.NewGuid(), Guid.NewGuid(), withSpan ? Guid.NewGuid() : null,
            "FACT", "LIABILITY", "summary", "ADMITTED", 0.9m, "DYNAMIC_LLM", null, null,
            docId, "file.pdf", "DEMAND", 1, withSpan ? 3 : null, null, withSpan ? "quoted passage" : null, 0.9m);

    private static LegalPropositionSupportDto Support(Guid propId, Guid evidenceId, string relation)
        => new(Guid.NewGuid(), propId, evidenceId, relation, null);

    private static LegalEvidenceGraphPropositionDto Proposition(
        Guid id, string factState, IReadOnlyCollection<LegalPropositionSupportDto> support)
        => new(id, Guid.NewGuid(), "The driver ran the red light.", factState, "DYNAMIC_LLM", 0.8m, true, support);

    [Fact]
    public void AdmittedSpanBackedSupport_Passes()
    {
        var propId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var props = new[] { Proposition(propId, LegalFactStates.Supported, [Support(propId, evidenceId, LegalDocumentRelationshipTypes.Supports)]) };
        var evidence = new[] { Evidence(evidenceId, docId, withSpan: true) };

        var result = LegalPropositionIntegrityBridge.Evaluate(props, evidence).Single();

        Assert.Equal(propId, result.PropositionId);
        Assert.Equal(LegalIntegrityDisposition.Pass, result.Disposition);
        Assert.True(result.StructuralEligible);
        Assert.True(result.EvidenceEligible);
    }

    [Fact]
    public void UnsupportedProposition_IsPassWithUnresolved_NotBlocked()
    {
        var propId = Guid.NewGuid();
        var props = new[] { Proposition(propId, LegalFactStates.Alleged, []) };

        var result = LegalPropositionIntegrityBridge.Evaluate(props, []).Single();

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.True(result.StructuralEligible);
        Assert.False(result.EvidenceEligible);
        Assert.Contains(LegalIntegrityReasonCodes.NoVerifiedSupport, result.ReasonCodes);
    }

    [Fact]
    public void SupportWithoutSpan_RequiresRepair()
    {
        var propId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var props = new[] { Proposition(propId, LegalFactStates.Supported, [Support(propId, evidenceId, LegalDocumentRelationshipTypes.Supports)]) };
        var evidence = new[] { Evidence(evidenceId, Guid.NewGuid(), withSpan: false) };

        var result = LegalPropositionIntegrityBridge.Evaluate(props, evidence).Single();

        Assert.Equal(LegalIntegrityDisposition.RepairRequired, result.Disposition);
        Assert.Contains(LegalIntegrityReasonCodes.SourceSpanMissing, result.ReasonCodes);
    }

    [Fact]
    public void DisputedProposition_UsesOpenChallengePath()
    {
        var propId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var props = new[] { Proposition(propId, LegalFactStates.Disputed, [Support(propId, evidenceId, LegalDocumentRelationshipTypes.Supports)]) };
        var evidence = new[] { Evidence(evidenceId, Guid.NewGuid(), withSpan: true) };

        var result = LegalPropositionIntegrityBridge.Evaluate(props, evidence).Single();

        // A disputed proposition is structurally unresolved (open challenge) → not a clean structural leaf.
        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.False(result.StructuralEligible);
        Assert.Contains(LegalIntegrityReasonCodes.BudgetExhausted, result.ReasonCodes);
    }

    [Fact]
    public void MultipleSupportsFromSingleDocument_AreCorrelated()
    {
        var propId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var props = new[]
        {
            Proposition(propId, LegalFactStates.Supported,
            [
                Support(propId, e1, LegalDocumentRelationshipTypes.Supports),
                Support(propId, e2, LegalDocumentRelationshipTypes.Supports)
            ])
        };
        var evidence = new[] { Evidence(e1, docId, true), Evidence(e2, docId, true) };
        var docSource = new Dictionary<Guid, Guid> { [e1] = docId, [e2] = docId };

        var result = LegalPropositionIntegrityBridge.Evaluate(props, evidence, docSource).Single();

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.False(result.EvidenceEligible);
        Assert.Contains(LegalIntegrityReasonCodes.CorrelatedEvidence, result.ReasonCodes);
    }

    [Fact]
    public void EmptyGraph_ReturnsNoResults()
    {
        Assert.Empty(LegalPropositionIntegrityBridge.Evaluate(null, null));
        Assert.Empty(LegalPropositionIntegrityBridge.Evaluate([], []));
    }
}
