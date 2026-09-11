using System.Text.Json;
using Legal.Application;
using Legal.Application.Features.Intelligence.Science;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── POLOXI Math V1 LLM contract tests ────────────────────────────────────────────────────────────
// Pin the wire contract between the model and the pipeline: every MATH_* schema is a valid object
// schema with matching required/properties, and a schema-shaped JSON payload round-trips into the
// corresponding proposal DTO using the same case-insensitive options the pipeline uses.
public sealed class MathContractTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void PromptContracts_CoverEveryMathPromptCode()
    {
        string[] mathCodes =
        [
            IntelligencePromptCodes.MathProblemContract,
            IntelligencePromptCodes.MathStrategyProposal,
            IntelligencePromptCodes.MathSolutionDerivation,
            IntelligencePromptCodes.MathStepVerification,
            IntelligencePromptCodes.MathCounterexampleSearch,
            IntelligencePromptCodes.MathAnswerExtraction,
            IntelligencePromptCodes.MathSelfConsistency,
            IntelligencePromptCodes.MathAnswerComposer,
        ];

        Assert.Equal(mathCodes.Length, MathPromptContracts.All.Count);
        foreach (var code in mathCodes)
        {
            Assert.True(MathPromptContracts.All.ContainsKey(code), $"Missing contract for {code}.");
            var contract = MathPromptContracts.All[code];
            Assert.False(string.IsNullOrWhiteSpace(contract.Instructions));
            Assert.True(IntelligencePromptDefaults.All.ContainsKey(code), $"Missing default prompt for {code}.");
        }
    }

    [Theory]
    [InlineData(IntelligencePromptCodes.MathProblemContract)]
    [InlineData(IntelligencePromptCodes.MathStrategyProposal)]
    [InlineData(IntelligencePromptCodes.MathSolutionDerivation)]
    [InlineData(IntelligencePromptCodes.MathStepVerification)]
    [InlineData(IntelligencePromptCodes.MathCounterexampleSearch)]
    [InlineData(IntelligencePromptCodes.MathAnswerExtraction)]
    [InlineData(IntelligencePromptCodes.MathSelfConsistency)]
    [InlineData(IntelligencePromptCodes.MathAnswerComposer)]
    public void EverySchema_IsAnObjectSchemaWithMatchingRequired(string promptCode)
    {
        var (schemaJson, _) = MathPromptContracts.All[promptCode];
        using var doc = JsonDocument.Parse(schemaJson);
        var root = doc.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.True(root.TryGetProperty("properties", out var properties));
        Assert.True(root.TryGetProperty("required", out var required));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        var propertyNames = properties.EnumerateObject().Select(p => p.Name).ToHashSet();
        foreach (var name in required.EnumerateArray())
            Assert.Contains(name.GetString()!, propertyNames);
        Assert.Equal(propertyNames.Count, required.GetArrayLength());
    }

    [Fact]
    public void ProblemContract_PayloadRoundTrips()
    {
        const string json = """
        {
          "statement": "Prove n(n+1) is even for all integers n.",
          "problemType": "PROVE",
          "domain": "MATHEMATICS",
          "subdomain": "number theory",
          "givens": ["n is an integer"],
          "unknowns": [],
          "assumptions": [],
          "constraints": [],
          "definitions": ["even means divisible by 2"],
          "quantifiers": ["for all n"],
          "target": "n(n+1) is even",
          "allowedMethods": ["case analysis"],
          "requiresExternalKnowledge": false,
          "requiresSymbolicComputation": false,
          "requiresNumericalComputation": false,
          "requiresFormalVerification": true
        }
        """;

        var dto = JsonSerializer.Deserialize<MathProblemContractProposal>(json, Options);

        Assert.NotNull(dto);
        Assert.Equal("PROVE", dto!.ProblemType);
        Assert.Equal("MATHEMATICS", dto.Domain);
        Assert.Single(dto.Givens);
        Assert.True(dto.RequiresFormalVerification);
        Assert.Empty(dto.Unknowns);
    }

    [Fact]
    public void SolutionDerivation_PayloadRoundTrips()
    {
        const string json = """
        {
          "nodes": [
            { "id": "n1", "nodeType": "ASSUMPTION", "statement": "n is an integer", "justification": null }
          ],
          "edges": [
            { "fromNodeId": "n1", "toNodeId": "n2", "relation": "IMPLIES" }
          ],
          "obligations": [
            {
              "id": "o1",
              "parentProofNodeId": "n1",
              "statement": "2 + 2 == 4",
              "verificationMethod": "ALGEBRAIC",
              "normalizedClaim": "2 + 2 == 4",
              "expectedRelation": "EQUAL",
              "left": 4.0,
              "right": 4.0,
              "testInputs": [],
              "discoveryConfidence": 0.9
            }
          ],
          "answer": {
            "answerValue": "even",
            "answerType": "BOOLEAN",
            "canonicalForm": "true",
            "supportingNodeIds": ["n1"]
          }
        }
        """;

        var dto = JsonSerializer.Deserialize<MathSolutionDerivationProposal>(json, Options);

        Assert.NotNull(dto);
        Assert.Single(dto!.Nodes);
        Assert.Equal("IMPLIES", dto.Edges[0].Relation);
        Assert.Equal("ALGEBRAIC", dto.Obligations[0].VerificationMethod);
        Assert.Equal("EQUAL", dto.Obligations[0].ExpectedRelation);
        Assert.NotNull(dto.Answer);
        Assert.Equal("BOOLEAN", dto.Answer!.AnswerType);
    }

    [Fact]
    public void AnswerComposer_PayloadRoundTrips()
    {
        const string json = """
        {
          "outcome": "PROVEN",
          "finalAnswer": "n(n+1) is always even",
          "solutionSummary": "By case analysis on parity of n.",
          "keySteps": ["Case n even", "Case n odd"],
          "remainingUncertainty": null,
          "decisiveCheck": "Both cases yield a factor of 2."
        }
        """;

        var dto = JsonSerializer.Deserialize<MathAnswerComposerProposal>(json, Options);

        Assert.NotNull(dto);
        Assert.Equal("PROVEN", dto!.Outcome);
        Assert.Equal(2, dto.KeySteps.Count);
        Assert.Null(dto.RemainingUncertainty);
    }
}
