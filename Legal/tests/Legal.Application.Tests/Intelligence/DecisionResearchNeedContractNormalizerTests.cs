using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionResearchNeedContractNormalizerTests
{
    [Fact]
    public void EmptyModelProposal_IsClassifiedAsIncompleteStructuredOutput()
    {
        var proposal = new DecisionResearchSemanticProposal();

        Assert.Empty(proposal.Leaves);
        Assert.Equal(
            DecisionModelOutputClassifications.IncompleteStructuredProposal,
            DecisionModelOutputClassifications.IncompleteStructuredProposal);
        Assert.NotEqual(
            DecisionModelOutputClassifications.ModelReturnedClarificationQuestion,
            DecisionModelOutputClassifications.IncompleteStructuredProposal);
    }

    [Fact]
    public void ModelOutputIntegrityFailure_IsNotUserClarification()
    {
        Assert.Equal("MODEL_OUTPUT_INTEGRITY_FAILURE", DecisionModelOutputClassifications.ModelOutputIntegrityFailure);
        Assert.NotEqual(
            DecisionModelOutputClassifications.ModelOutputIntegrityFailure,
            "USER_CLARIFICATION_REQUIRED");
    }

    [Theory]
    [InlineData("gpt-6-astra")]
    [InlineData("gpt-4.1-mini")]
    public void SupportedModelsUseTheSameIntegrityGovernance(string modelCode)
    {
        var attempt = new DecisionResearchTransformationAttemptDto(
            Attempt: 1,
            ModelCode: modelCode,
            Status: "MODEL_OUTPUT_INTEGRITY_FAILURE",
            LeavesProduced: 0,
            Disposition: "REPAIR",
            Defects: ["MODEL_OUTPUT_INTEGRITY_FAILURE", "INCOMPLETE_STRUCTURED_PROPOSAL"],
            Leaves: [])
        {
            OutputClassification = DecisionModelOutputClassifications.IncompleteStructuredProposal,
            BlockingReason = "The model response did not satisfy the required structured Decision Contract.",
        };

        Assert.Equal(DecisionModelOutputClassifications.IncompleteStructuredProposal, attempt.OutputClassification);
        Assert.Equal("REPAIR", attempt.Disposition);
        Assert.NotEqual("USER_CLARIFICATION_REQUIRED", attempt.Status);
    }

    // A researchable MATTER_EVIDENCE leaf with a valid declarative proposition but a missing SearchQuery must
    // be self-healed with a deterministic retrieval expression derived from that proposition — never invented.
    [Fact]
    public void MatterLeafMissingSearchQuery_IsHealedFromProposition()
    {
        var proposal = new DecisionResearchSemanticProposal
        {
            Leaves =
            [
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "ME1",
                    ResearchNeedType = DecisionResearchNeedTypes.MatterEvidence,
                    ResearchQuestion = "What does the recorded vehicle speed indicate?",
                    Proposition = "The vehicle speed is documented in a witness statement and accident reconstruction report.",
                    SourceClass = DecisionResearchSourceClasses.MatterDocument,
                    Researchable = true,
                    SearchQuery = null,
                    SearchConcepts = [],
                    CandidateDiscrimination = ["C1", "C2"],
                },
            ],
        };

        var normalized = DecisionResearchNeedContractNormalizer.Normalize(proposal);
        var leaf = normalized.Leaves[0];

        Assert.False(string.IsNullOrWhiteSpace(leaf.SearchQuery));
        Assert.NotEmpty(leaf.SearchConcepts);
        Assert.Contains("vehicle", leaf.SearchQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("speed", leaf.SearchQuery, StringComparison.OrdinalIgnoreCase);
        // Stopwords must not leak into the derived retrieval expression.
        Assert.DoesNotContain(" is ", $" {leaf.SearchQuery} ", StringComparison.OrdinalIgnoreCase);
    }

    // Legal-authority leaves are NEVER healed — a missing legal-authority SearchQuery remains a defect so the
    // gate is not weakened for public-research leaves.
    [Fact]
    public void LegalAuthorityLeafMissingSearchQuery_IsNotHealed()
    {
        var proposal = new DecisionResearchSemanticProposal
        {
            Leaves =
            [
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "LR1",
                    ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
                    ResearchQuestion = "What negligence standard governs vehicle speed?",
                    Proposition = "A driver breaches the duty of care by exceeding a safe speed under the circumstances.",
                    SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                    Researchable = true,
                    SearchQuery = null,
                    SearchConcepts = [],
                    AuthorityKinds = ["CASE_LAW"],
                    CandidateDiscrimination = ["C1", "C2"],
                },
            ],
        };

        var normalized = DecisionResearchNeedContractNormalizer.Normalize(proposal);

        Assert.True(string.IsNullOrWhiteSpace(normalized.Leaves[0].SearchQuery));
        Assert.Empty(normalized.Leaves[0].SearchConcepts);
    }

    // End-to-end: a valid LEGAL_RULE leaf plus a matter leaf missing its SearchQuery must, after
    // normalization, pass the gate so the legal-rule lane can be exercised instead of stopping at the gate.
    [Fact]
    public void NormalizedMatterLeaf_LetsProposalPassGate()
    {
        var proposal = new DecisionResearchSemanticProposal
        {
            Leaves =
            [
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "LR1",
                    ResearchNeedType = DecisionResearchNeedTypes.LegalRule,
                    ResearchQuestion = "What standard governs a negligence claim from excessive vehicle speed?",
                    Proposition = "A driver who exceeds a reasonable speed for conditions breaches the duty of care owed to others.",
                    SourceClass = DecisionResearchSourceClasses.LegalAuthority,
                    Researchable = true,
                    SearchQuery = "California negligence duty of care excessive speed",
                    SearchConcepts = ["negligence", "duty of care", "excessive speed"],
                    AuthorityKinds = ["CASE_LAW", "STATUTE"],
                    CandidateDiscrimination = ["C1", "C2"],
                },
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "MF1",
                    ResearchNeedType = DecisionResearchNeedTypes.MatterFact,
                    ResearchQuestion = "What speed was the vehicle traveling?",
                    Proposition = "The vehicle traveled above the posted speed limit at the time of the collision.",
                    SourceClass = DecisionResearchSourceClasses.MatterDocument,
                    Researchable = true,
                    SearchQuery = null,
                    SearchConcepts = [],
                    CandidateDiscrimination = ["C1", "C2"],
                },
                new DecisionResearchSemanticLeaf
                {
                    ResearchKey = "APP1",
                    ResearchNeedType = DecisionResearchNeedTypes.Application,
                    ResearchQuestion = "Do the established facts satisfy the negligence standard?",
                    Proposition = "The established facts satisfy the negligence standard under the verified rule.",
                    SourceClass = DecisionResearchSourceClasses.None,
                    Researchable = false,
                    ApplicationDeferred = true,
                    CandidateDiscrimination = ["C1", "C2"],
                    Requires = ["LR1", "MF1"],
                },
            ],
        };

        var normalized = DecisionResearchNeedContractNormalizer.Normalize(proposal);
        var result = new DecisionResearchabilityGate().Evaluate(normalized);

        Assert.True(result.IsAcceptable);
        Assert.DoesNotContain(result.Defects, defect => defect.EndsWith("SEARCH_QUERY_MISSING", StringComparison.Ordinal));
        Assert.Contains(result.ResearchableLeaves, leaf => leaf.ResearchKey == "LR1");
        Assert.Contains(result.ResearchableLeaves, leaf => leaf.ResearchKey == "MF1");
    }
}
