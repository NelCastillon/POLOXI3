using System.Collections.Generic;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI APR §5 structural validation. Verifies the pure LegalAdaptivePropositionResolver independently
// validates the LLM's atomicity/decomposition proposal: it accepts genuinely atomic leaves (T01), splits
// known material compounds (T02), never forces a split on a single qualified clause (T04), preserves
// parent fidelity and rejects fidelity-losing children as REPAIR_REQUIRED (T05/T06), reuses canonical
// duplicates (T07), excludes evidence probes from the hierarchy (T08), and leaves budget-deferred or
// challenged nodes explicitly UNRESOLVED rather than silently atomic (T12).
public sealed class LegalAdaptivePropositionResolverTests
{
    private static LegalAprAssessmentInput Atomic(string text, bool contextSufficient = true, bool challenge = false)
        => new(text, LlmProposedAtomic: true, ProposedChildTexts: [], ContextSufficient: contextSufficient, HasOpenChallenge: challenge);

    private static LegalAprAssessmentInput Compound(string text, params string[] children)
        => new(text, LlmProposedAtomic: false, ProposedChildTexts: children);

    // ── T01: a genuinely atomic L3 proposition stops (ATOMIC_ACCEPTED) ──
    [Fact]
    public void T01_AtomicProposition_IsAccepted_AndStops()
    {
        var result = LegalAdaptivePropositionResolver.Assess(Atomic("The driver ran the red light."));

        Assert.Equal(LegalAprDisposition.AtomicAccepted, result.Disposition);
        Assert.Equal(LegalPropositionStructuralStates.AtomicAccepted, result.StructuralStateCode);
        Assert.True(result.IsAtomicAccepted);
    }

    // ── T02: a compound proposition splits into validated children (COMPOUND) ──
    [Fact]
    public void T02_CompoundProposition_Splits()
    {
        var result = LegalAdaptivePropositionResolver.Assess(
            Compound(
                "The driver was speeding and ran the red light.",
                "The driver was speeding.",
                "The driver ran the red light."));

        Assert.Equal(LegalAprDisposition.Compound, result.Disposition);
        Assert.Equal(LegalPropositionStructuralStates.Compound, result.StructuralStateCode);
        Assert.Contains(LegalAprReasonCodes.MaterialSplitDetected, result.ReasonCodes);
    }

    // ── T02b: POLOXI independently detects a material split even when the LLM proposed atomic ──
    [Fact]
    public void T02b_IndependentMaterialSplit_OverridesLlmAtomicProposal()
    {
        var result = LegalAdaptivePropositionResolver.Assess(
            Atomic("The driver was speeding and the driver used a phone."));

        Assert.Equal(LegalAprDisposition.Compound, result.Disposition);
        Assert.Contains(LegalAprReasonCodes.MaterialSplitDetected, result.ReasonCodes);
    }

    // ── T04: no forced split of a single qualified clause with an incidental conjunction-free predicate ──
    [Fact]
    public void T04_SingleQualifiedClause_IsNotForciblySplit()
    {
        var result = LegalAdaptivePropositionResolver.Assess(
            Atomic("The driver was speeding before the intersection."));

        Assert.Equal(LegalAprDisposition.AtomicAccepted, result.Disposition);
    }

    [Fact]
    public void T04b_NounConjunction_IsNotAMaterialSplit()
    {
        Assert.False(LegalAdaptivePropositionResolver.HasKnownMaterialSplit("The cars and trucks were present."));
    }

    // ── T05: parent fidelity — dropping a negation qualifier fails and yields REPAIR_REQUIRED ──
    [Fact]
    public void T05_DroppedNegation_FailsParentFidelity_AndRequiresRepair()
    {
        var result = LegalAdaptivePropositionResolver.Assess(
            Compound(
                "The driver did not signal and did not brake.",
                "The driver signaled.",
                "The driver braked."));

        Assert.Equal(LegalAprDisposition.RepairRequired, result.Disposition);
        Assert.Equal(LegalPropositionStructuralStates.RepairRequired, result.StructuralStateCode);
        Assert.Contains(LegalAprReasonCodes.ParentFidelityFailed, result.ReasonCodes);
    }

    // ── T06: exception/condition qualifiers are retained across the children (fidelity preserved) ──
    [Fact]
    public void T06_RetainedException_PreservesParentFidelity()
    {
        var preserved = LegalAdaptivePropositionResolver.ValidateParentFidelity(
            "The carrier is liable unless the exclusion applies.",
            ["The carrier is liable.", "The result holds unless the exclusion applies."]);

        Assert.True(preserved);
    }

    [Fact]
    public void T06b_DroppedException_FailsParentFidelity()
    {
        var preserved = LegalAdaptivePropositionResolver.ValidateParentFidelity(
            "The carrier is liable unless the exclusion applies.",
            ["The carrier is liable.", "The exclusion is in the policy."]);

        Assert.False(preserved);
    }

    // ── T07: a duplicate of an existing canonical proposition reuses identity (no re-registration) ──
    [Fact]
    public void T07_DuplicateOfCanonical_ReusesIdentity()
    {
        var input = new LegalAprAssessmentInput(
            "The driver ran the red light.",
            LlmProposedAtomic: true,
            ProposedChildTexts: [],
            IsDuplicateOfCanonical: true);

        var result = LegalAdaptivePropositionResolver.Assess(input);

        Assert.Equal(LegalAprDisposition.Duplicate, result.Disposition);
        Assert.Contains(LegalAprReasonCodes.DuplicateCanonical, result.ReasonCodes);
    }

    // ── T08: an evidence probe is not a hierarchy child ──
    [Theory]
    [InlineData("Obtain the driver's phone records for the collision window.")]
    [InlineData("Verify the timestamp on the traffic-camera footage.")]
    [InlineData("Subpoena the carrier's claims file.")]
    public void T08_EvidenceProbe_IsExcludedFromHierarchy(string probe)
    {
        Assert.True(LegalAdaptivePropositionResolver.IsEvidenceProbe(probe));

        var result = LegalAdaptivePropositionResolver.Assess(Atomic(probe));

        Assert.Equal(LegalAprDisposition.EvidenceProbe, result.Disposition);
        Assert.Equal(LegalPropositionStructuralStates.Invalid, result.StructuralStateCode);
    }

    [Fact]
    public void T08b_Assertion_IsNotAnEvidenceProbe()
    {
        Assert.False(LegalAdaptivePropositionResolver.IsEvidenceProbe("The driver ran the red light."));
    }

    // ── T12: a budget-exhausted atomic candidate stays UNRESOLVED, never silently ATOMIC_ACCEPTED ──
    [Fact]
    public void T12_BudgetExhausted_LeavesNodeUnresolved()
    {
        var result = LegalAdaptivePropositionResolver.Assess(
            Atomic("The driver ran the red light."),
            budgetExhausted: true);

        Assert.Equal(LegalAprDisposition.Unresolved, result.Disposition);
        Assert.Equal(LegalPropositionStructuralStates.Uncertain, result.StructuralStateCode);
        Assert.Contains(LegalAprReasonCodes.BudgetExhausted, result.ReasonCodes);
    }

    // ── T12b: an open challenge blocks acceptance (unresolved) ──
    [Fact]
    public void T12b_OpenChallenge_BlocksAcceptance()
    {
        var result = LegalAdaptivePropositionResolver.Assess(Atomic("The driver ran the red light.", challenge: true));

        Assert.Equal(LegalAprDisposition.Unresolved, result.Disposition);
        Assert.Contains(LegalAprReasonCodes.OpenChallenge, result.ReasonCodes);
    }

    // ── Context insufficiency surfaces AMBIGUOUS (clarify), not a silent atomic accept ──
    [Fact]
    public void InsufficientContext_IsAmbiguous()
    {
        var result = LegalAdaptivePropositionResolver.Assess(Atomic("It happened.", contextSufficient: false));

        Assert.Equal(LegalAprDisposition.Ambiguous, result.Disposition);
        Assert.Equal(LegalPropositionStructuralStates.Uncertain, result.StructuralStateCode);
    }
}
