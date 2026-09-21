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