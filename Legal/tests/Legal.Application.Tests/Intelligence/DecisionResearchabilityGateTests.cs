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
                    ResearchQuestion = "Do established law and facts create a genuine dispute of material fact?",
                    Proposition = "Established law and facts create a genuine dispute of material fact.",
                    SourceClass = DecisionResearchSourceClasses.None,
                    Researchable = false,
                    ApplicationDeferred = true,
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
            ResearchQuestion = "Who bears the burden of establishing the claimed FLSA exemption?",
            Proposition = "The employer bears the burden of establishing the claimed FLSA exemption.",
            SourceClass = DecisionResearchSourceClasses.LegalAuthority,
            Researchable = true,
            SearchQuery = "FLSA exemption burden of proof employer",
            SearchConcepts = ["FLSA exemption", "burden of proof"],
            AuthorityKinds = ["CASE_LAW", "STATUTE"],
            CandidateDiscrimination = ["C1", "C2"],
        };
        var matterFact = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "MATTER_FACT_1",
            ResearchNeedType = DecisionResearchNeedTypes.MatterFact,
            ResearchQuestion = "What duties does the employee claim to have performed?",
            Proposition = "The employee performed the operational duties described in the employee's account.",
            SourceClass = DecisionResearchSourceClasses.MatterDocument,
            Researchable = true,
            SearchQuery = "employee actual duties testimony job description",
            SearchConcepts = ["employee testimony", "actual duties"],
            CandidateDiscrimination = ["C1", "C2"],
        };
        var application = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "APPLICATION_1",
            ResearchNeedType = DecisionResearchNeedTypes.Application,
            ResearchQuestion = "Do the established duties satisfy the verified exemption standard?",
            Proposition = "The established duties satisfy the verified exemption standard.",
            SourceClass = DecisionResearchSourceClasses.None,
            Researchable = false,
            ApplicationDeferred = true,
            CandidateDiscrimination = ["C1", "C2"],
            Requires = ["LEGAL_RULE_1", "MATTER_FACT_1"],
        };
        var proposal = new DecisionResearchSemanticProposal { Leaves = [legalRule, matterFact, application] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.True(result.IsAcceptable);
        Assert.Equal([legalRule, matterFact], result.ResearchableLeaves);
    }

    [Fact]
    public void InterrogativeFrontierContainer_DoesNotPoisonValidResearchableLeaves()
    {
        var hierarchy = ValidHierarchy();
        var frontier = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "C3.B1.frontier",
            ResearchNeedType = DecisionResearchNeedTypes.Application,
            ResearchQuestion = "Whether Emily's causal negligence exceeds the governing comparative-fault threshold?",
            Proposition = "Whether Emily's causal negligence exceeds the governing comparative-fault threshold remains unresolved.",
            SourceClass = DecisionResearchSourceClasses.None,
            Researchable = false,
            ApplicationDeferred = true,
            CandidateDiscrimination = ["C3", "C4"],
            Requires = hierarchy.Leaves.Where(leaf => leaf.Researchable).Select(leaf => leaf.ResearchKey).ToArray(),
        };
        var proposal = hierarchy with { Leaves = [frontier, .. hierarchy.Leaves] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.True(result.IsAcceptable);
        Assert.DoesNotContain(result.Defects, defect => defect.Contains("PROPOSITION_MUST_BE_DECLARATIVE", StringComparison.Ordinal));
        Assert.NotEmpty(result.ResearchableLeaves);
    }

    [Fact]
    public void RootDerivedResearchQuestion_WithF0Key_DoesNotPoisonValidResearchableLeaves()
    {
        var hierarchy = ValidHierarchy();
        var children = hierarchy.Leaves
            .Select(leaf => leaf with { ParentResearchKey = "F0" })
            .ToArray();
        var frontier = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "F0",
            ResearchNeedType = DecisionResearchNeedTypes.Derived,
            ResearchQuestion = "Whether Emily's causal negligence exceeds the applicable comparison threshold?",
            Proposition = "Whether Emily's causal negligence exceeds the applicable comparison threshold remains unresolved.",
            SourceClass = DecisionResearchSourceClasses.None,
            Researchable = false,
            ApplicationDeferred = true,
            CandidateDiscrimination = ["C3", "C4"],
            Requires = children.Where(leaf => leaf.Researchable).Select(leaf => leaf.ResearchKey).ToArray(),
        };

        var result = new DecisionResearchabilityGate().Evaluate(
            new DecisionResearchSemanticProposal { Leaves = [frontier, .. children] });

        Assert.True(result.IsAcceptable);
        Assert.DoesNotContain("F0:PROPOSITION_MUST_BE_DECLARATIVE", result.Defects);
        Assert.Equal(children.Count(leaf => leaf.Researchable), result.ResearchableLeaves.Count);
    }

    [Fact]
    public void FrontierContainer_WithInsufficientDependencies_RemainsRejected()
    {
        var hierarchy = ValidHierarchy();
        var frontier = new DecisionResearchSemanticLeaf
        {
            ResearchKey = "C3.B1.frontier",
            ResearchNeedType = DecisionResearchNeedTypes.Application,
            ResearchQuestion = "Whether the threshold is met?",
            Proposition = "Whether the threshold is met remains unresolved.",
            SourceClass = DecisionResearchSourceClasses.None,
            Researchable = false,
            ApplicationDeferred = true,
            CandidateDiscrimination = ["C3", "C4"],
            Requires = [hierarchy.Leaves[0].ResearchKey],
        };

        var result = new DecisionResearchabilityGate().Evaluate(hierarchy with { Leaves = [frontier, .. hierarchy.Leaves] });

        Assert.False(result.IsAcceptable);
        Assert.Contains("C3.B1.frontier:DERIVED_NODE_DEPENDENCIES_INSUFFICIENT", result.Defects);
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
                    ResearchQuestion = "Does sufficient evidence create a genuine dispute regarding misclassification?",
                    Proposition = "There is sufficient evidence to raise a genuine dispute of material fact regarding misclassification.",
                    SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                    Researchable = true,
                    SearchQuery = "misclassification genuine dispute sufficient evidence",
                    SearchConcepts = ["misclassification", "genuine dispute"],
                    AuthorityKinds = ["CASE_LAW"],
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
                    ResearchQuestion = "Is there conflicting evidence about employees' job duties in this matter?",
                    Proposition = "Whether there is conflicting evidence about employees' job duties in this matter.",
                    SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                    Researchable = true,
                    SearchQuery = "conflicting evidence employee job duties",
                    SearchConcepts = ["conflicting evidence", "job duties"],
                    AuthorityKinds = ["CASE_LAW"],
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

    [Fact]
    public void ResearchQuestion_CannotBeUsedAsVerificationProposition()
    {
        var valid = ValidHierarchy();
        var legal = valid.Leaves[0] with
        {
            Proposition = "Which binding decisions define the executive exemption's primary-duty requirement?",
        };
        var proposal = valid with { Leaves = [legal, .. valid.Leaves.Skip(1)] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.False(result.IsAcceptable);
        Assert.Contains(result.Defects, defect => defect.EndsWith(":PROPOSITION_MUST_BE_DECLARATIVE", StringComparison.Ordinal));
    }

    [Fact]
    public void SettlementEnforcement_QuestionShapedApplication_DoesNotBlockValidResearchLeaves()
    {
        var hierarchy = ValidHierarchy();
        var application = hierarchy.Leaves[2] with
        {
            ResearchKey = "C5.B1.application",
            ResearchQuestion = "Whether the asserted settlement is enforceable?",
            Proposition = "Whether the asserted settlement is enforceable remains deferred pending verified authority and matter evidence.",
            Requires = [hierarchy.Leaves[0].ResearchKey, hierarchy.Leaves[1].ResearchKey],
        };

        var result = new DecisionResearchabilityGate().Evaluate(
            hierarchy with { Leaves = [hierarchy.Leaves[0], hierarchy.Leaves[1], application] });

        Assert.True(result.IsAcceptable);
        Assert.DoesNotContain("C5.B1.application:PROPOSITION_MUST_BE_DECLARATIVE", result.Defects);
        Assert.Equal(2, result.ResearchableLeaves.Count);
    }

    [Fact]
    public void SettlementEnforcement_QuestionShapedResearchableLeaf_RemainsRejectedForBoundedRepair()
    {
        var hierarchy = ValidHierarchy();
        var legalRule = hierarchy.Leaves[0] with
        {
            ResearchKey = "C5.B1.legal-rule",
            Proposition = "Whether an asserted settlement is enforceable?",
        };
        var proposal = hierarchy with { Leaves = [legalRule, hierarchy.Leaves[1], hierarchy.Leaves[2]] };

        var result = new DecisionResearchabilityGate().Evaluate(proposal);

        Assert.False(result.IsAcceptable);
        Assert.Contains("C5.B1.legal-rule:PROPOSITION_MUST_BE_DECLARATIVE", result.Defects);
        Assert.Equal(DecisionResearchNeedDisposition.Repair, DecisionResearchNeedRepairPlanner.Diagnose(result.Defects));
    }

    private static DecisionResearchSemanticProposal ValidHierarchy() => new()
    {
        Leaves =
        [
            new DecisionResearchSemanticLeaf
            {
                ResearchKey = "LEGAL_RULE_1",
                ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
                ResearchQuestion = "What standard determines whether disputed duties are material at summary judgment?",
                Proposition = "A dispute over actual job duties is material at summary judgment when those duties determine whether an exemption element is satisfied.",
                SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                Researchable = true,
                SearchQuery = "FLSA actual job duties material dispute summary judgment exemption",
                SearchConcepts = ["actual job duties", "material dispute", "summary judgment", "FLSA exemption"],
                AuthorityKinds = ["CASE_LAW", "REGULATION"],
                CandidateDiscrimination = ["C1", "C2"],
            },
            new DecisionResearchSemanticLeaf
            {
                ResearchKey = "MATTER_FACT_1",
                ResearchNeedType = DecisionResearchNeedTypes.MatterFact,
                ResearchQuestion = "What duties does the employee claim to have performed?",
                Proposition = "The employee performed the operational duties described in the employee's testimony.",
                SourceClass = DecisionResearchSourceClasses.MatterDocument,
                Researchable = true,
                SearchQuery = "employee testimony actual duties",
                SearchConcepts = ["employee testimony", "actual duties"],
                CandidateDiscrimination = ["C1", "C2"],
            },
            new DecisionResearchSemanticLeaf
            {
                ResearchKey = "APPLICATION_1",
                ResearchNeedType = DecisionResearchNeedTypes.Application,
                ResearchQuestion = "Do the established duties create a material dispute under the verified standard?",
                Proposition = "The established duties create a material dispute under the verified standard.",
                SourceClass = DecisionResearchSourceClasses.None,
                Researchable = false,
                ApplicationDeferred = true,
                CandidateDiscrimination = ["C1", "C2"],
                Requires = ["LEGAL_RULE_1", "MATTER_FACT_1"],
            },
        ],
    };
}