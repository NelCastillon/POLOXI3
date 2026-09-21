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
    public void AtomicLegalRuleLeaf_IsAcceptedAsSourceResolvable()
    {
        var leaf = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "LEGAL_RULE_1",
            ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
            Proposition = "Who bears the burden of establishing the claimed FLSA exemption?",
            SourceClass = DecisionResearchSourceClasses.LegalAuthority,
            Researchable = true,
            CandidateDiscrimination = ["C1", "C2"],
        };
        var proposal = new DecisionResearchSemanticProposal { Leaves = [leaf] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.True(result.IsAcceptable);
        Assert.Same(leaf, Assert.Single(result.ResearchableLeaves));
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
}