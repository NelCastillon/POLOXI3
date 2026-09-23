using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionResearchNeedRepairPlannerTests
{
    [Fact]
    public void NoDefects_DiagnosesRepair()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose([]);

        Assert.Equal(DecisionResearchNeedDisposition.Repair, disposition);
    }

    [Fact]
    public void QuestionProposition_DiagnosesRepair()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:PROPOSITION_MUST_BE_DECLARATIVE"]);

        Assert.Equal(DecisionResearchNeedDisposition.Repair, disposition);
    }

    [Fact]
    public void MixedLeaf_DiagnosesDecompose()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:MIXED_LEAF_MUST_BE_DECOMPOSED"]);

        Assert.Equal(DecisionResearchNeedDisposition.Decompose, disposition);
    }

    [Fact]
    public void InvalidResearchNeedType_DiagnosesDecompose()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:RESEARCH_NEED_TYPE_INVALID"]);

        Assert.Equal(DecisionResearchNeedDisposition.Decompose, disposition);
    }

    [Fact]
    public void MatterFindingRoutedToAuthority_DiagnosesRouteMatter()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:MATTER_FINDING_NOT_PUBLICLY_RESEARCHABLE"]);

        Assert.Equal(DecisionResearchNeedDisposition.RouteMatter, disposition);
    }

    [Fact]
    public void SourceClassMismatch_DiagnosesRouteMatter()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:SOURCE_CLASS_MISMATCH"]);

        Assert.Equal(DecisionResearchNeedDisposition.RouteMatter, disposition);
    }

    [Fact]
    public void DecomposeWinsOverRouteMatter()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:SOURCE_CLASS_MISMATCH", "LEAF_2:MIXED_LEAF_MUST_BE_DECOMPOSED"]);

        Assert.Equal(DecisionResearchNeedDisposition.Decompose, disposition);
    }

    [Fact]
    public void RouteMatterWinsOverRepair()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:SEARCH_QUERY_MISSING", "LEAF_2:MATTER_FINDING_NOT_PUBLICLY_RESEARCHABLE"]);

        Assert.Equal(DecisionResearchNeedDisposition.RouteMatter, disposition);
    }

    [Fact]
    public void AllStructuralUnrecoverable_DiagnosesUnresolved()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["NO_SOURCE_RESOLVABLE_LEAVES", "INSUFFICIENT_SOURCE_RESOLVABLE_LEAVES"]);

        Assert.Equal(DecisionResearchNeedDisposition.Unresolved, disposition);
    }

    [Fact]
    public void StructuralDefectWithActionableDefect_DoesNotDiagnoseUnresolved()
    {
        // A repairable defect alongside a structural one must NOT collapse to UNRESOLVED — the bounded
        // repair attempt should still run.
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["NO_SOURCE_RESOLVABLE_LEAVES", "LEAF_1:PROPOSITION_MUST_BE_DECLARATIVE"]);

        Assert.NotEqual(DecisionResearchNeedDisposition.Unresolved, disposition);
    }

    [Fact]
    public void UnknownDefect_FallsThroughToRepair_NeverAccept()
    {
        var disposition = DecisionResearchNeedRepairPlanner.Diagnose(
            ["LEAF_1:SOME_FUTURE_DEFECT_CODE"]);

        Assert.Equal(DecisionResearchNeedDisposition.Repair, disposition);
    }

    [Fact]
    public void BuildDirective_Decompose_MentionsSourceClassRouting()
    {
        var directive = DecisionResearchNeedRepairPlanner.BuildDirective(
            DecisionResearchNeedDisposition.Decompose, ["LEAF_1:MIXED_LEAF_MUST_BE_DECOMPOSED"]);

        Assert.Contains("DECOMPOSE", directive);
        Assert.Contains("MATTER_DOCUMENT", directive);
        Assert.Contains("LEAF_1:MIXED_LEAF_MUST_BE_DECOMPOSED", directive);
    }

    [Fact]
    public void BuildDirective_RouteMatter_MentionsReRouting()
    {
        var directive = DecisionResearchNeedRepairPlanner.BuildDirective(
            DecisionResearchNeedDisposition.RouteMatter, ["LEAF_1:MATTER_FINDING_NOT_PUBLICLY_RESEARCHABLE"]);

        Assert.Contains("RE-ROUTE", directive);
        Assert.Contains("MATTER_DOCUMENT", directive);
    }

    [Fact]
    public void BuildDirective_Repair_MentionsDeclarativeRequirement()
    {
        var directive = DecisionResearchNeedRepairPlanner.BuildDirective(
            DecisionResearchNeedDisposition.Repair, ["LEAF_1:PROPOSITION_MUST_BE_DECLARATIVE"]);

        Assert.Contains("REPAIR ONLY THESE DEFECTS", directive);
        Assert.Contains("declarative", directive);
    }
}
