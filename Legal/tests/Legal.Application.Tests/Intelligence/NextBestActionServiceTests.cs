using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Verifies the HYBRID boundary of Next Best Action: the LLM only phrases actions, while the deterministic
// frontier stays authoritative. Two properties are proven here:
//   1. The LLM phrasing call is PINNED to the Astra synthesis model (gpt-6-astra), matching the Overview
//      candidate-landscape narrative instead of drifting onto the base route.
//   2. When the LLM is unavailable or produces nothing admissible, the service degrades to DETERMINISTIC
//      fallback actions built straight from the unresolved ResolutionRequirement frontier (still traceable
//      to an exact proposition and its ADV) rather than failing or implying the matter is resolved.
public sealed class NextBestActionServiceTests
{
    private const string AstraModelCode = "gpt-6-astra";

    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static NextBestActionService CreateService(IAiProviderRouter router, MatterPropositionInformationValueResult scored)
        => new(
            new FakeIvService(scored),
            new EmptyGraphCorpusRepository(),
            new NoMatterDecisionRepository(),
            new NoPackResolver(),
            router);

    [Fact]
    public async Task ProviderFailure_DegradesToDeterministicActions_TraceableToProposition()
    {
        var p = Prop("P1", "defendant ran the red light", LegalFactStates.Alleged, informationValue: 0.90m, decisionImpact: 0.80m);
        var service = CreateService(new ThrowingRouter(), Scored(p));

        var result = await service.GenerateAsync(Tenant, Matter, Session, null);

        var action = Assert.Single(result.Actions);
        Assert.Equal(DeterministicGuid("P1"), action.PropositionId);
        Assert.Equal("defendant ran the red light", action.TargetProposition);
        Assert.Null(result.NotRunReason);
    }

    [Fact]
    public async Task EmptyProposals_DegradesToDeterministicActions()
    {
        var p = Prop("P1", "the road was wet", LegalFactStates.Alleged, informationValue: 0.70m, decisionImpact: 0.70m);
        var service = CreateService(new JsonRouter("""{"actions":[]}"""), Scored(p));

        var result = await service.GenerateAsync(Tenant, Matter, Session, null);

        Assert.Single(result.Actions);
        Assert.Null(result.NotRunReason);
    }

    [Fact]
    public async Task LlmProposalCall_IsPinnedToAstraModel()
    {
        var p = Prop("P1", "defendant had an unobstructed view", LegalFactStates.Alleged, informationValue: 0.80m, decisionImpact: 0.80m);
        var router = new CapturingRouter("""{"actions":[]}""");
        var service = CreateService(router, Scored(p));

        await service.GenerateAsync(Tenant, Matter, Session, null);

        Assert.Equal(AstraModelCode, router.LastModelCodeOverride);
    }

    [Fact]
    public async Task AdmissibleProposal_IsSelected_OverFallback()
    {
        var p = Prop("P1", "the brakes were defective", LegalFactStates.Alleged, informationValue: 0.90m, decisionImpact: 0.90m);
        var json = JsonSerializer.Serialize(new
        {
            actions = new[]
            {
                new
                {
                    propositionId = DeterministicGuid("P1").ToString(),
                    title = "Obtain the vehicle brake inspection report",
                    informationSought = "The post-collision brake inspection findings from the certified mechanic",
                    context = (string?)null,
                },
            },
        });
        var service = CreateService(new JsonRouter(json), Scored(p));

        var result = await service.GenerateAsync(Tenant, Matter, Session, null);

        var action = Assert.Single(result.Actions);
        Assert.Equal("Obtain the vehicle brake inspection report", action.Title);
        Assert.Null(result.NotRunReason);
    }

    private static MatterPropositionInformationValueResult Scored(params MatterPropositionInformationValue[] props)
        => new(Matter, props, false);

    private static MatterPropositionInformationValue Prop(
        string id, string text, string factState, decimal informationValue, decimal decisionImpact)
        => new(
            DeterministicGuid(id),
            text,
            factState,
            "UNVERIFIED",
            informationValue,
            IsEssential: false,
            IsExecutable: true,
            Materiality: decisionImpact,
            Uncertainty: 0.5m,
            DecisionImpact: decisionImpact,
            Discrimination: 0.5m,
            RedundancyPenalty: 0m);

    private static Guid DeterministicGuid(string id)
    {
        var bytes = new byte[16];
        var raw = System.Text.Encoding.UTF8.GetBytes(id);
        Array.Copy(raw, bytes, Math.Min(raw.Length, 16));
        return new Guid(bytes);
    }

    private static AiGenerationResult Generation(string content)
        => new(content, null, 0, 0, null, "req", TimeSpan.Zero, "test", AstraModelCode);

    private sealed class FakeIvService(MatterPropositionInformationValueResult result) : IMatterPropositionInformationValueService
    {
        public Task<MatterPropositionInformationValueResult> ScoreAsync(
            Guid tenantId, Guid matterId, Guid decisionSessionId, Guid? actorUserId, bool persist = true, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class ThrowingRouter : IAiProviderRouter
    {
        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("provider unavailable");

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private class JsonRouter(string content) : IAiProviderRouter
    {
        private readonly string _content = content;

        public virtual Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Generation(_content));

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingRouter(string content) : JsonRouter(content)
    {
        public string? LastModelCodeOverride { get; private set; }

        public override Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
        {
            LastModelCodeOverride = modelCodeOverride;
            return base.GenerateAsync(tenantId, featureCode, systemPrompt, userPrompt, outputSchemaJson, correlationId, executionContext, modelCodeOverride, cancellationToken);
        }
    }

    private sealed class EmptyGraphCorpusRepository : StubCorpusRepository
    {
        public override Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegalMatterEvidenceGraphDto(matterId, 0, [], [], []));
    }

    private sealed class NoMatterDecisionRepository : StubDecisionRepository
    {
        public override Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<DecisionMatterDto?>(null);
    }

    private sealed class NoPackResolver : IDomainPackResolver
    {
        public Task<ResolvedDomainPack> ResolveAsync(Guid tenantId, string? packCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolvedDomainPack(packCode ?? string.Empty, string.Empty, [], [], [], [], []));
    }
}
