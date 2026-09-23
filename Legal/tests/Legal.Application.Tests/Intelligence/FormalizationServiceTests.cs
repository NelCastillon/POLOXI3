using System.Text.Json;
using Legal.Application;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Science;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── POLOXI Formalization Gate tests ─────────────────────────────────────────────────────────────
// Pin the Research → Formalize → Math contract: the gate turns a research idea into a precise Proof
// Contract, degrades honestly (never throws) when the model route is unavailable, and always produces
// a self-contained Math Solver handoff derived from the contract - not the raw research narrative.
public sealed class FormalizationServiceTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Formalize_ValidProposal_ReturnsContractAndHandoff()
    {
        var router = new ScriptedAiRouter(Serialize(new ProofContract
        {
            CandidateId = "TRANSFER_COHERENCE",
            ObjectType = "REGULARITY_CRITERION",
            Statement = "If the transfer coherence functional is bounded on [0,T) then u extends past T.",
            Assumptions = ["u is a smooth solution on [0,T)"],
            AllowedTools = ["energy identity"],
            ForbiddenAssumptions = ["assuming the global bound"],
            ProofStandard = "A rigorous a priori estimate.",
            Falsification = "A bounded-coherence solution that blows up.",
            Confidence = 0.5,
        }));

        var service = new FormalizationService(router, new PassThroughPromptCatalog(), NullLogger<FormalizationService>.Instance);

        var response = await service.FormalizeAsync(new FormalizationRequest(Guid.NewGuid(), Guid.NewGuid(), "Phase coherence might be the obstruction.", "corr-1"));

        Assert.True(response.ModelAvailable);
        Assert.Equal("REGULARITY_CRITERION", response.Contract.ObjectType);
        Assert.Contains("CLAIM:", response.MathSolverHandoff);
        Assert.Contains("energy identity", response.MathSolverHandoff);
        Assert.Contains("FORBIDDEN", response.MathSolverHandoff);
    }

    [Fact]
    public async Task Formalize_ModelUnavailable_DegradesToEchoContract()
    {
        var service = new FormalizationService(new UnavailableAiRouter(), new PassThroughPromptCatalog(), NullLogger<FormalizationService>.Instance);

        var response = await service.FormalizeAsync(new FormalizationRequest(Guid.NewGuid(), Guid.NewGuid(), "A vague research hunch.", "corr-2"));

        Assert.False(response.ModelAvailable);
        Assert.Equal("CONJECTURE", response.Contract.ObjectType);
        Assert.Equal(0d, response.Contract.Confidence);
        Assert.Contains("A vague research hunch.", response.Contract.Statement);
    }

    [Fact]
    public async Task Formalize_EmptyResearchIdea_Throws()
    {
        var service = new FormalizationService(new ScriptedAiRouter("{}"), new PassThroughPromptCatalog(), NullLogger<FormalizationService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FormalizeAsync(new FormalizationRequest(Guid.NewGuid(), Guid.NewGuid(), "   ", "corr-3")));
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private sealed class PassThroughPromptCatalog : IPromptCatalog
    {
        public Task<string> GetSystemPromptAsync(Guid tenantId, string promptCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(promptCode);
    }

    private sealed class ScriptedAiRouter(string json) : IAiProviderRouter
    {
        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiGenerationResult(json, json, 0, 0, null, "req", TimeSpan.Zero, "fake", "fake-model"));

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnavailableAiRouter : IAiProviderRouter
    {
        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default) =>
            throw new AiProviderUnavailableException(featureCode, "No provider configured.");

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
