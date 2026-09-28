using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI v4.0 Light Evidence Graph control. Verifies the pure graph derives the exact per-node signals the
// §9 Proposition Integrity Gate consumes: admitted support presence, source-span/entailment admissibility,
// correlated-single-source detection, conflict presence, and derivation-cycle detection. Missing evidence is
// never treated as false — it simply produces an unsupported node — and correlated support never silently
// counts as independent corroboration.
public sealed class LegalLightEvidenceGraphTests
{
    private static LegalEvidenceProposition Prop(string id, bool authorityRequired = false, bool authorityVerified = false)
        => new(id, authorityRequired, authorityVerified);

    private static LegalEvidenceNodeSignals SignalsFor(
        string propId,
        IReadOnlyCollection<LegalEvidenceProposition> props,
        IReadOnlyCollection<LegalEvidenceUnit> units,
        IReadOnlyCollection<LegalEvidenceBinding> bindings)
        => LegalLightEvidenceGraph.DeriveSignals(props, units, bindings).Single(s => s.PropositionId == propId);

    // ── Admissible single support → HasSupportingEvidence, span present, entailment satisfied ──
    [Fact]
    public void AdmissibleSupport_ProducesSupportedNode()
    {
        var props = new[] { Prop("P1") };
        var units = new[] { new LegalEvidenceUnit("E1", "DOC1", "p3:120-160") };
        var bindings = new[] { new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "E1", "P1") };

        var s = SignalsFor("P1", props, units, bindings);

        Assert.True(s.HasSupportingEvidence);
        Assert.True(s.HasSourceSpan);
        Assert.True(s.EntailmentSatisfied);
        Assert.False(s.IsCorrelatedOnly);
        Assert.False(s.HasConflictingEvidence);
    }

    // ── No binding at all → unsupported (NOT false); admissibility flags stay clean ──
    [Fact]
    public void NoSupport_ProducesUnsupportedNode_NotFalse()
    {
        var props = new[] { Prop("P1") };

        var s = SignalsFor("P1", props, [], []);

        Assert.False(s.HasSupportingEvidence);
        Assert.True(s.HasSourceSpan);        // nothing failed admissibility — there was simply nothing
        Assert.True(s.EntailmentSatisfied);
        Assert.Contains(LegalEvidenceNodeNotes.NoAdmittedSupport, s.Notes);
    }

    // ── A support binding whose evidence lacks a source span → not admitted, span-missing surfaced ──
    [Fact]
    public void SupportWithoutSpan_IsNotAdmitted_AndReportsSpanMissing()
    {
        var props = new[] { Prop("P1") };
        var units = new[] { new LegalEvidenceUnit("E1", "DOC1", SourceSpan: null) };
        var bindings = new[] { new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "E1", "P1") };

        var s = SignalsFor("P1", props, units, bindings);

        Assert.False(s.HasSupportingEvidence);
        Assert.False(s.HasSourceSpan);
        Assert.Contains(LegalEvidenceNodeNotes.SourceSpanMissing, s.Notes);
    }

    // ── A support binding that fails entailment → not admitted, entailment failure surfaced ──
    [Fact]
    public void SupportFailingEntailment_IsNotAdmitted_AndReportsEntailmentFailed()
    {
        var props = new[] { Prop("P1") };
        var units = new[] { new LegalEvidenceUnit("E1", "DOC1", "p1:1-40") };
        var bindings = new[] { new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "E1", "P1", EntailmentSatisfied: false) };

        var s = SignalsFor("P1", props, units, bindings);

        Assert.False(s.HasSupportingEvidence);
        Assert.False(s.EntailmentSatisfied);
        Assert.Contains(LegalEvidenceNodeNotes.EntailmentFailed, s.Notes);
    }

    // ── Multiple admitted supports all from ONE document → correlated-only (not independent) ──
    [Fact]
    public void MultipleSupportsFromSingleSource_IsCorrelatedOnly()
    {
        var props = new[] { Prop("P1") };
        var units = new[]
        {
            new LegalEvidenceUnit("E1", "DOC1", "p1:1-20"),
            new LegalEvidenceUnit("E2", "DOC1", "p4:60-90")
        };
        var bindings = new[]
        {
            new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "E1", "P1"),
            new LegalEvidenceBinding("B2", LegalEvidenceBindingRoles.Supports, "E2", "P1")
        };

        var s = SignalsFor("P1", props, units, bindings);

        Assert.True(s.HasSupportingEvidence);
        Assert.True(s.IsCorrelatedOnly);
        Assert.Contains(LegalEvidenceNodeNotes.CorrelatedSingleSource, s.Notes);
    }

    // ── Multiple admitted supports from DISTINCT documents → independent corroboration, not correlated ──
    [Fact]
    public void MultipleSupportsFromDistinctSources_IsNotCorrelated()
    {
        var props = new[] { Prop("P1") };
        var units = new[]
        {
            new LegalEvidenceUnit("E1", "DOC1", "p1:1-20"),
            new LegalEvidenceUnit("E2", "DOC2", "p4:60-90")
        };
        var bindings = new[]
        {
            new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "E1", "P1"),
            new LegalEvidenceBinding("B2", LegalEvidenceBindingRoles.Supports, "E2", "P1")
        };

        var s = SignalsFor("P1", props, units, bindings);

        Assert.True(s.HasSupportingEvidence);
        Assert.False(s.IsCorrelatedOnly);
    }

    // ── A CONFLICTS binding is surfaced independently of support ──
    [Fact]
    public void ConflictBinding_IsReported()
    {
        var props = new[] { Prop("P1") };
        var units = new[] { new LegalEvidenceUnit("E1", "DOC1", "p2:10-30") };
        var bindings = new[] { new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Conflicts, "E1", "P1") };

        var s = SignalsFor("P1", props, units, bindings);

        Assert.True(s.HasConflictingEvidence);
        Assert.False(s.HasSupportingEvidence);
        Assert.Contains(LegalEvidenceNodeNotes.ConflictPresent, s.Notes);
    }

    // ── A support binding referencing an unknown evidence id is ignored but noted ──
    [Fact]
    public void UnknownEvidenceReference_IsIgnoredAndNoted()
    {
        var props = new[] { Prop("P1") };
        var bindings = new[] { new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "MISSING", "P1") };

        var s = SignalsFor("P1", props, [], bindings);

        Assert.False(s.HasSupportingEvidence);
        Assert.Contains(LegalEvidenceNodeNotes.UnknownEvidenceReference, s.Notes);
    }

    // ── Authority requirement flows through unchanged for the gate to consume ──
    [Fact]
    public void AuthorityFlags_FlowThrough()
    {
        var props = new[] { Prop("P1", authorityRequired: true, authorityVerified: false) };

        var s = SignalsFor("P1", props, [], []);

        Assert.True(s.AuthorityRequired);
        Assert.False(s.AuthorityVerified);
    }

    // ── Derivation cycle detection: an acyclic chain has no cycle ──
    [Fact]
    public void AcyclicDerivationChain_HasNoCycle()
    {
        var props = new[] { Prop("P1"), Prop("P2"), Prop("P3") };
        var bindings = new[]
        {
            new LegalEvidenceBinding("D1", LegalEvidenceBindingRoles.Derives, "P1", "P2"),
            new LegalEvidenceBinding("D2", LegalEvidenceBindingRoles.Derives, "P2", "P3")
        };

        Assert.False(LegalLightEvidenceGraph.HasDerivationCycle(props, bindings));
    }

    // ── Derivation cycle detection: a back-edge forms a cycle ──
    [Fact]
    public void CyclicDerivation_IsDetected()
    {
        var props = new[] { Prop("P1"), Prop("P2"), Prop("P3") };
        var bindings = new[]
        {
            new LegalEvidenceBinding("D1", LegalEvidenceBindingRoles.Derives, "P1", "P2"),
            new LegalEvidenceBinding("D2", LegalEvidenceBindingRoles.Derives, "P2", "P3"),
            new LegalEvidenceBinding("D3", LegalEvidenceBindingRoles.Derives, "P3", "P1")
        };

        Assert.True(LegalLightEvidenceGraph.HasDerivationCycle(props, bindings));
    }

    // ── A self-derivation is a cycle ──
    [Fact]
    public void SelfDerivation_IsCycle()
    {
        var props = new[] { Prop("P1") };
        var bindings = new[] { new LegalEvidenceBinding("D1", LegalEvidenceBindingRoles.Derives, "P1", "P1") };

        Assert.True(LegalLightEvidenceGraph.HasDerivationCycle(props, bindings));
    }

    // ── SUPPORTS bindings never count toward cycle detection (only DERIVES edges do) ──
    [Fact]
    public void SupportEdgesDoNotFormDerivationCycles()
    {
        var props = new[] { Prop("P1"), Prop("P2") };
        var units = new[] { new LegalEvidenceUnit("E1", "DOC1", "p1:1-10") };
        var bindings = new[]
        {
            new LegalEvidenceBinding("B1", LegalEvidenceBindingRoles.Supports, "E1", "P1"),
            new LegalEvidenceBinding("D1", LegalEvidenceBindingRoles.Derives, "P1", "P2")
        };

        Assert.False(LegalLightEvidenceGraph.HasDerivationCycle(props, bindings));
    }

    // ── End-to-end: derived signals map cleanly onto the Integrity Gate input and pass with unresolved ──
    [Fact]
    public void DerivedSignals_FeedIntegrityGate_UnsupportedAtom_IsPassWithUnresolved()
    {
        var props = new[] { Prop("P1") };
        var s = SignalsFor("P1", props, [], []);

        var aprAtomic = new LegalAprAssessment(
            LegalAprDisposition.AtomicAccepted, LegalPropositionStructuralStates.AtomicAccepted, [LegalAprReasonCodes.AtomicAccepted]);

        var gateInput = new LegalPropositionIntegrityInput(
            s.PropositionId,
            aprAtomic,
            HasSupportingEvidence: s.HasSupportingEvidence,
            HasSourceSpan: s.HasSourceSpan,
            EntailmentSatisfied: s.EntailmentSatisfied,
            IsCorrelatedOnly: s.IsCorrelatedOnly,
            AuthorityRequired: s.AuthorityRequired,
            AuthorityVerified: s.AuthorityVerified);

        var result = LegalPropositionIntegrityGate.Evaluate(gateInput);

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.True(result.StructuralEligible);
        Assert.False(result.EvidenceEligible);
    }
}
