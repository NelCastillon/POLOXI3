using Legal.Infrastructure.Services;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class AzureOpenAiReasoningEffortTests
{
    [Theory]
    [InlineData("DECISION_DISCOVERY")]
    [InlineData("DECISION_GRAPH")]
    [InlineData("DECISION_VERIFY")]
    [InlineData("DECISION_RESEARCH_NEED")]
    [InlineData("EVIDENCE_SEMANTIC_VERIFY")]
    public void AstraSemanticStage_UsesSupportedLowEffort(string featureCode)
    {
        var effort = AzureOpenAiProvider.ResolveReasoningEffort("gpt-6-astra", featureCode);

        Assert.Equal("low", effort);
    }

    [Fact]
    public void AstraAnswerStage_RetainsMediumEffort()
    {
        var effort = AzureOpenAiProvider.ResolveReasoningEffort("gpt-6-astra", "DECISION_ANSWER");

        Assert.Equal("medium", effort);
    }

    [Fact]
    public void CompatibleReasoningModel_MechanicalStageRetainsMinimalEffort()
    {
        var effort = AzureOpenAiProvider.ResolveReasoningEffort("gpt-5.6-sol", "WIDE_HIERARCHY_STEP");

        Assert.Equal("minimal", effort);
    }

    [Fact]
    public void AstraDecisionGraph_StartsWithEightThousandTokenBudget()
    {
        var budget = AzureOpenAiProvider.ResolveInitialOutputBudget(
            "gpt-6-astra", "DECISION_GRAPH", 65536, "low");

        Assert.Equal(8000, budget);
    }

    [Theory]
    [InlineData("DECISION_DISCOVERY")]
    [InlineData("DECISION_VERIFY")]
    [InlineData("DECISION_RESEARCH_NEED")]
    [InlineData("EVIDENCE_SEMANTIC_VERIFY")]
    public void AstraSmallerSemanticStage_RetainsFourThousandTokenBudget(string featureCode)
    {
        var budget = AzureOpenAiProvider.ResolveInitialOutputBudget(
            "gpt-6-astra", featureCode, 65536, "low");

        Assert.Equal(4000, budget);
    }

    [Fact]
    public void StandardModel_RetainsConfiguredBudget()
    {
        var budget = AzureOpenAiProvider.ResolveInitialOutputBudget(
            "gpt-4.1-mini", "DECISION_GRAPH", 12000, "minimal");

        Assert.Equal(12000, budget);
    }
}
