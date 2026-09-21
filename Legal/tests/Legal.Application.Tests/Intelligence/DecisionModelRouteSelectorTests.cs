using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionModelRouteSelectorTests
{
    private static readonly DecisionModelRouteDto AstraResearch = Route("DECISION_RESEARCH_NEED", "gpt-6-astra", 10);
    private static readonly DecisionModelRouteDto AstraVerify = Route("EVIDENCE_SEMANTIC_VERIFY", "gpt-6-astra", 10);
    private static readonly DecisionModelRouteDto MiniRepair = Route(DecisionModelRouteSelector.SchemaRepairFeatureCode, "gpt-4.1-mini", 10);
    private static readonly DecisionModelRouteDto MiniDefault = Route(DecisionModelRouteSelector.DefaultFeatureCode, "gpt-4.1-mini", 100);

    [Fact]
    public void Auto_SelectsExactSubstantiveAstraRoute()
    {
        var selected = DecisionModelRouteSelector.Select(
            [MiniDefault, AstraVerify, MiniRepair, AstraResearch],
            "DECISION_RESEARCH_NEED");

        Assert.Same(AstraResearch, selected);
    }

    [Fact]
    public void SchemaRepair_SelectsDedicatedMiniRoute()
    {
        var selected = DecisionModelRouteSelector.Select(
            [AstraVerify, MiniDefault, MiniRepair],
            DecisionModelRouteSelector.SchemaRepairFeatureCode);

        Assert.Same(MiniRepair, selected);
    }

    [Fact]
    public void ExplicitModelOverride_PreservesRequestedModel()
    {
        var selected = DecisionModelRouteSelector.Select(
            [AstraResearch, MiniDefault],
            "DECISION_RESEARCH_NEED",
            "gpt-4.1-mini");

        Assert.Same(MiniDefault, selected);
    }

    [Fact]
    public void MissingTaskRoute_FallsBackToDecisionDefault()
    {
        var selected = DecisionModelRouteSelector.Select(
            [AstraVerify, MiniDefault],
            "DECISION_GRAPH");

        Assert.Same(MiniDefault, selected);
    }

    private static DecisionModelRouteDto Route(string featureCode, string modelCode, int priority) => new(
        featureCode,
        "AZURE_OPENAI",
        modelCode,
        modelCode,
        "env://AMS_AZURE_OPENAI_ENDPOINT",
        "env://AMS_AZURE_OPENAI_KEY",
        "2024-10-21",
        600,
        65536,
        0m,
        priority);
}
