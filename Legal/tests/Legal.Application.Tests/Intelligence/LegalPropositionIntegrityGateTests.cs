using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI §9 Proposition Integrity Gate control. Verifies the pure gate composes the §5 APR assessment and
// §6 coverage finding with evidence/authority/snapshot signals into the four-valued disposition and the two
// independent eligibility flags. Key invariants: an unsupported accepted atom is PASS_WITH_UNRESOLVED, not
// BLOCKED (missing evidence is not false — T31); a material coverage gap BLOCKS readiness (T36); and
// StructuralEligible/EvidenceEligible never collapse into one opaque verdict.
public sealed class LegalPropositionIntegrityGateTests
{
    private static LegalAprAssessment Atomic()
        => new(LegalAprDisposition.AtomicAccepted, LegalPropositionStructuralStates.AtomicAccepted, [LegalAprReasonCodes.AtomicAccepted]);

    private static LegalAprAssessment Compound()
        => new(LegalAprDisposition.Compound, LegalPropositionStructuralStates.Compound, [LegalAprReasonCodes.MaterialSplitDetected]);

    private static LegalAprAssessment RepairApr()
        => new(LegalAprDisposition.RepairRequired, LegalPropositionStructuralStates.RepairRequired, [LegalAprReasonCodes.ParentFidelityFailed]);

    private static LegalCoverageFinding CoverageWithMaterialGap()
        => new(LegalCoverageScopes.L2Factors, LegalCoveragePolicies.RequiredComponents, ["A"],
            [new LegalCoverageGap("DEFENSE", IsMaterial: true, LegalCoverageReasonCodes.RequiredComponentMissing, "missing")],
            []);

    // ── Fully verified atomic proposition → PASS, both eligibility flags true ──
    [Fact]
    public void VerifiedAtomicProposition_Passes()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: true);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.Pass, result.Disposition);
        Assert.True(result.StructuralEligible);
        Assert.True(result.EvidenceEligible);
    }

    // ── T31: an accepted atom with NO supporting evidence is PASS_WITH_UNRESOLVED, never BLOCKED ──
    [Fact]
    public void T31_UnsupportedAcceptedAtom_IsUnresolved_NotBlocked()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: false);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.True(result.StructuralEligible);   // structurally sound
        Assert.False(result.EvidenceEligible);    // but no admissible support yet
        Assert.Contains(LegalIntegrityReasonCodes.NoVerifiedSupport, result.ReasonCodes);
    }

    // ── T36: a material coverage gap BLOCKS readiness ──
    [Fact]
    public void T36_MaterialCoverageGap_BlocksReadiness()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), CoverageWithMaterialGap(), HasSupportingEvidence: true);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.Blocked, result.Disposition);
        Assert.Contains(LegalIntegrityReasonCodes.MaterialCoverageGap, result.ReasonCodes);
    }

    // ── A compound structure is a repairable structural defect ──
    [Fact]
    public void CompoundStructure_RequiresRepair_AndIsStructurallyIneligible()
    {
        var input = new LegalPropositionIntegrityInput("N1", Compound(), HasSupportingEvidence: true);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.RepairRequired, result.Disposition);
        Assert.False(result.StructuralEligible);
        Assert.Contains(LegalIntegrityReasonCodes.StructuralCompound, result.ReasonCodes);
    }

    // ── Parent-fidelity failure from APR maps to REPAIR_REQUIRED ──
    [Fact]
    public void ParentFidelityFailure_RequiresRepair()
    {
        var input = new LegalPropositionIntegrityInput("N1", RepairApr());

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.RepairRequired, result.Disposition);
        Assert.Contains(LegalIntegrityReasonCodes.ParentFidelityFailed, result.ReasonCodes);
    }

    // ── Mandatory-repair conditions (security/contract/cycle/stale) BLOCK and outrank everything ──
    [Theory]
    [InlineData(true, false, false, false, LegalIntegrityReasonCodes.SecurityViolation)]
    [InlineData(false, true, false, false, LegalIntegrityReasonCodes.ContractInvalid)]
    [InlineData(false, false, true, false, LegalIntegrityReasonCodes.GraphCycle)]
    [InlineData(false, false, false, true, LegalIntegrityReasonCodes.StaleSnapshot)]
    public void MandatoryRepairConditions_Block(bool security, bool contract, bool cycle, bool stale, string expectedReason)
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(),
            SecurityViolation: security, ContractInvalid: contract, HasGraphCycle: cycle, IsSnapshotStale: stale,
            HasSupportingEvidence: true);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.Blocked, result.Disposition);
        Assert.Contains(expectedReason, result.ReasonCodes);
    }

    // ── A claimed support without a source span is a repairable binding defect (never silent support) ──
    [Fact]
    public void SupportWithoutSourceSpan_RequiresRepair()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: true, HasSourceSpan: false);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.RepairRequired, result.Disposition);
        Assert.False(result.EvidenceEligible);
        Assert.Contains(LegalIntegrityReasonCodes.SourceSpanMissing, result.ReasonCodes);
    }

    // ── A binding that fails entailment is a repairable defect ──
    [Fact]
    public void SupportFailingEntailment_RequiresRepair()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: true, EntailmentSatisfied: false);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.RepairRequired, result.Disposition);
        Assert.Contains(LegalIntegrityReasonCodes.BindingEntailmentFailed, result.ReasonCodes);
    }

    // ── Correlated-only support keeps the node evaluable but not evidence-eligible ──
    [Fact]
    public void CorrelatedOnlySupport_IsUnresolved()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: true, IsCorrelatedOnly: true);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.False(result.EvidenceEligible);
        Assert.Contains(LegalIntegrityReasonCodes.CorrelatedEvidence, result.ReasonCodes);
    }

    // ── A legal proposition requiring authority that is unverified stays unresolved ──
    [Fact]
    public void UnverifiedAuthority_IsUnresolved()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(),
            HasSupportingEvidence: true, AuthorityRequired: true, AuthorityVerified: false);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.False(result.EvidenceEligible);
        Assert.Contains(LegalIntegrityReasonCodes.AuthorityUnverified, result.ReasonCodes);
    }

    // ── An unresolved condition (§7 CONDITIONAL_ON) keeps the node evaluable ──
    [Fact]
    public void UnresolvedCondition_IsUnresolved()
    {
        var input = new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: true, HasUnresolvedCondition: true);

        var result = LegalPropositionIntegrityGate.Evaluate(input);

        Assert.Equal(LegalIntegrityDisposition.PassWithUnresolved, result.Disposition);
        Assert.Contains(LegalIntegrityReasonCodes.ConditionUnresolved, result.ReasonCodes);
    }

    // ── EvaluateMany projects one result per input, preserving node ids ──
    [Fact]
    public void EvaluateMany_ReturnsOneResultPerInput()
    {
        var inputs = new[]
        {
            new LegalPropositionIntegrityInput("N1", Atomic(), HasSupportingEvidence: true),
            new LegalPropositionIntegrityInput("N2", Compound())
        };

        var results = LegalPropositionIntegrityGate.EvaluateMany(inputs);

        Assert.Equal(2, results.Count);
        Assert.Equal("N1", results[0].NodeId);
        Assert.Equal(LegalIntegrityDisposition.Pass, results[0].Disposition);
        Assert.Equal("N2", results[1].NodeId);
        Assert.Equal(LegalIntegrityDisposition.RepairRequired, results[1].Disposition);
    }
}
