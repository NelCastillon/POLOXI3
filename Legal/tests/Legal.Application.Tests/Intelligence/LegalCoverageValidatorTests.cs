using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI §6 MECE coverage control. Verifies the pure LegalCoverageValidator reports a missing required
// item as a MATERIAL gap (T09 missing defense), allows coexisting remedies without forcing exclusivity
// (T10), classifies declared overlaps rather than auto-rejecting them (T11), and defaults to open-world
// (absence is non-blocking debt) when exhaustiveness cannot be justified.
public sealed class LegalCoverageValidatorTests
{
    private static LegalCoverageItem Item(string code, bool represented, bool required = false, string[]? overlaps = null, bool excluded = false)
        => new(code, code, required, represented, overlaps, excluded);

    // ── T09: a required defense that is neither represented nor excluded is a MATERIAL gap ──
    [Fact]
    public void T09_MissingRequiredDefense_IsMaterialGap()
    {
        var items = new[]
        {
            Item("LIABILITY", represented: true, required: true),
            Item("COMPARATIVE_FAULT_DEFENSE", represented: false, required: true)
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L2Factors, LegalCoveragePolicies.RequiredComponents, items);

        Assert.True(finding.HasMaterialGap);
        var gap = Assert.Single(finding.Gaps);
        Assert.Equal("COMPARATIVE_FAULT_DEFENSE", gap.ItemCode);
        Assert.True(gap.IsMaterial);
        Assert.Equal(LegalCoverageReasonCodes.RequiredComponentMissing, gap.ReasonCode);
    }

    // ── T09b: an explicitly excluded required item is NOT a gap (it was considered and ruled out) ──
    [Fact]
    public void T09b_ExplicitlyExcludedRequiredItem_IsNotAGap()
    {
        var items = new[]
        {
            Item("LIABILITY", represented: true, required: true),
            Item("ASSUMPTION_OF_RISK", represented: false, required: true, excluded: true)
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L2Factors, LegalCoveragePolicies.RequiredComponents, items);

        Assert.False(finding.HasMaterialGap);
        Assert.Empty(finding.Gaps);
    }

    // ── T09c: a required code named upstream but wholly absent from the scope is a material gap ──
    [Fact]
    public void T09c_RequiredCodeAbsentFromScope_IsMaterialGap()
    {
        var items = new[] { Item("LIABILITY", represented: true) };

        var finding = LegalCoverageValidator.Check(
            LegalCoverageScopes.L2Factors,
            LegalCoveragePolicies.RequiredComponents,
            items,
            expectedRequiredCodes: ["DAMAGES"]);

        Assert.True(finding.HasMaterialGap);
        Assert.Contains(finding.Gaps, g => g.ItemCode == "DAMAGES" && g.IsMaterial);
    }

    // ── T10: coexisting remedies under NON_EXCLUSIVE_SET are allowed — overlap is not a material gap ──
    [Fact]
    public void T10_CoexistingRemedies_AreAllowed_NotForcedExclusive()
    {
        var items = new[]
        {
            Item("INJUNCTION", represented: true, overlaps: ["DAMAGES"]),
            Item("DAMAGES", represented: true, overlaps: ["INJUNCTION"])
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L1Outcomes, LegalCoveragePolicies.NonExclusiveSet, items);

        Assert.False(finding.HasMaterialGap);
        Assert.All(finding.Overlaps, o => Assert.Equal(LegalCoverageOverlapKinds.PermittedCoexistence, o.OverlapKind));
        Assert.Equal(2, finding.Overlaps.Count);
    }

    // ── T11: overlaps are classified — permitted coexistence vs. an exclusive-partition violation ──
    [Fact]
    public void T11_Overlap_UnderExclusivePartition_IsPartitionViolation()
    {
        var items = new[]
        {
            Item("GRANTED", represented: true, overlaps: ["DENIED"]),
            Item("DENIED", represented: true, overlaps: ["GRANTED"])
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L1Outcomes, LegalCoveragePolicies.ExclusivePartition, items);

        Assert.True(finding.HasMaterialGap);
        Assert.All(finding.Overlaps, o => Assert.Equal(LegalCoverageOverlapKinds.PartitionViolation, o.OverlapKind));
        Assert.Contains(finding.Gaps, g => g.ReasonCode == LegalCoverageReasonCodes.PartitionOverlap && g.IsMaterial);
    }

    [Fact]
    public void T11b_ConditionalAlternatives_ClassifyOverlapAsPermitted()
    {
        var items = new[]
        {
            Item("PATH_A", represented: true, overlaps: ["PATH_B"]),
            Item("PATH_B", represented: true)
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L1Outcomes, LegalCoveragePolicies.ConditionalAlternatives, items);

        Assert.False(finding.HasMaterialGap);
        var overlap = Assert.Single(finding.Overlaps);
        Assert.Equal(LegalCoverageOverlapKinds.PermittedCoexistence, overlap.OverlapKind);
    }

    // ── Open-world default: a non-required absent item produces no material gap ──
    [Fact]
    public void OpenWorld_AbsenceIsNotAMaterialGap()
    {
        var items = new[]
        {
            Item("OUTCOME_A", represented: true),
            Item("OUTCOME_B", represented: false)
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L1Outcomes, LegalCoveragePolicies.OpenWorld, items);

        Assert.False(finding.HasMaterialGap);
    }

    // ── An unknown/blank policy falls back to open-world (fail-safe toward non-blocking). ──
    [Fact]
    public void UnknownPolicy_FallsBackToOpenWorld()
    {
        var items = new[] { Item("OUTCOME_A", represented: true) };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L1Outcomes, "NONSENSE_POLICY", items);

        Assert.Equal(LegalCoveragePolicies.OpenWorld, finding.PolicyCode);
        Assert.False(finding.HasMaterialGap);
    }

    [Fact]
    public void RepresentedItemCodes_ReflectOnlyRepresentedItems()
    {
        var items = new[]
        {
            Item("A", represented: true),
            Item("B", represented: false),
            Item("C", represented: true)
        };

        var finding = LegalCoverageValidator.Check(LegalCoverageScopes.L2Factors, LegalCoveragePolicies.NonExclusiveSet, items);

        Assert.Equal(["A", "C"], finding.RepresentedItemCodes);
    }
}
