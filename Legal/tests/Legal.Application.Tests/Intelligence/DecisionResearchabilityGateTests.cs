using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionResearchabilityGateTests
{
    [Fact]
    public void ApplicationConclusion_IsNeverRoutedToRetrieval()
    {
        var proposal = new DecisionResearchSemanticProposal
        {
            Leaves =
            [
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "APPLICATION_1",
                    ResearchNeedType = DecisionResearchNeedTypes.Application,
                    Proposition = "Whether established law and facts create a genuine dispute of material fact.",
                    SourceClass = DecisionResearchSourceClasses.None,
                    Researchable = false,
                    CandidateDiscrimination = ["C1", "C2"],
                },
            ],
        };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.False(result.IsAcceptable);
        Assert.Empty(result.ResearchableLeaves);
        Assert.Contains("NO_SOURCE_RESOLVABLE_LEAVES", result.Defects);
    }

    [Fact]
    public void SearchStyleHierarchy_IsAcceptedWithOnlyResolvableLeavesExecutable()
    {
        var legalRule = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "LEGAL_RULE_1",
            ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
            Proposition = "Who bears the burden of establishing the claimed FLSA exemption?",
            SourceClass = DecisionResearchSourceClasses.LegalAuthority,
            Researchable = true,
            CandidateDiscrimination = ["C1", "C2"],
        };
        var matterFact = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "MATTER_FACT_1",
            ResearchNeedType = DecisionResearchNeedTypes.MatterFact,
            Proposition = "What duties does the employee claim to have performed?",
            SourceClass = DecisionResearchSourceClasses.MatterDocument,
            Researchable = true,
            CandidateDiscrimination = ["C1", "C2"],
        };
        var application = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "APPLICATION_1",
            ResearchNeedType = DecisionResearchNeedTypes.Application,
            Proposition = "Do the established duties satisfy the verified exemption standard?",
            SourceClass = DecisionResearchSourceClasses.None,
            Researchable = false,
            CandidateDiscrimination = ["C1", "C2"],
            Requires = ["LEGAL_RULE_1", "MATTER_FACT_1"],
        };
        var proposal = new DecisionResearchSemanticProposal { Leaves = [legalRule, matterFact, application] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.True(result.IsAcceptable);
        Assert.Equal([legalRule, matterFact], result.ResearchableLeaves);
    }

    [Fact]
    public void AssertedApplicationConclusion_IsRejectedEvenWhenMarkedResearchable()
    {
        var proposal = new DecisionResearchSemanticProposal
        {
            Leaves =
            [
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "LEGAL_RULE_1",
                    ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
                    Proposition = "There is sufficient evidence to raise a genuine dispute of material fact regarding misclassification.",
                    SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                    Researchable = true,
                    CandidateDiscrimination = ["C1", "C2"],
                },
            ],
        };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.False(result.IsAcceptable);
        Assert.Empty(result.ResearchableLeaves);
        Assert.Contains(result.Defects, defect => defect.EndsWith(":ASSUMED_APPLICATION_CONCLUSION", StringComparison.Ordinal));
    }

    [Fact]
    public void MatterSpecificConflict_IsRejectedAsPublicLegalResearch()
    {
        var proposal = ValidHierarchy() with
        {
            Leaves =
            [
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "LEGAL_RULE_1",
                    ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
                    Proposition = "Whether there is conflicting evidence about employees' job duties in this matter.",
                    SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                    Researchable = true,
                    CandidateDiscrimination = ["C1", "C2"],
                },
                .. ValidHierarchy().Leaves.Skip(1),
            ],
        };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.False(result.IsAcceptable);
        Assert.Contains(result.Defects, defect => defect.EndsWith(":MATTER_FINDING_NOT_PUBLICLY_RESEARCHABLE", StringComparison.Ordinal));
    }

    [Fact]
    public void SingleResearchLeaf_IsRejectedAsShallowDecomposition()
    {
        var proposal = new DecisionResearchSemanticProposal { Leaves = [ValidHierarchy().Leaves[0]] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.False(result.IsAcceptable);
        Assert.Contains("SEMANTIC_HIERARCHY_TOO_SHALLOW", result.Defects);
        Assert.Contains("APPLICATION_NODE_MISSING", result.Defects);
    }

    private static DecisionResearchSemanticProposal ValidHierarchy() => new()
    {
        Leaves =
        [
            new DecisionResearchSemanticLeaf
            {
                ResearchKey = "LEGAL_RULE_1",
                ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
                Proposition = "What standard determines whether disputed duties are material at summary judgment?",
                SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                Researchable = true,
                CandidateDiscrimination = ["C1", "C2"],
            },
            new DecisionResearchSemanticLeaf
            {
                ResearchKey = "MATTER_FACT_1",
                ResearchNeedType = DecisionResearchNeedTypes.MatterFact,
                Proposition = "What duties does the employee claim to have performed?",
                SourceClass = DecisionResearchSourceClasses.MatterDocument,
                Researchable = true,
                CandidateDiscrimination = ["C1", "C2"],
            },
            new DecisionResearchSemanticLeaf
            {
                ResearchKey = "APPLICATION_1",
                ResearchNeedType = DecisionResearchNeedTypes.Application,
                Proposition = "Do the established duties create a material dispute under the verified standard?",
                SourceClass = DecisionResearchSourceClasses.None,
                Researchable = false,
                CandidateDiscrimination = ["C1", "C2"],
                Requires = ["LEGAL_RULE_1", "MATTER_FACT_1"],
            },
        ],
    };
}