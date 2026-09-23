using System.Text.Json;
using Legal.Application;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Science;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── POLOXI Math V1 orchestrator + mapper tests ─────────────────────────────────────────────────────
// Pin the runnable slice: the linear pipeline maps LLM proposals to domain, lets deterministic C#
// verification decide acceptance, and NEVER promotes an unverified claim to PROVEN. Uses a scripted
// fake AI router (prompt code -> canned JSON) and a pass-through prompt catalog.
public sealed class MathReasoningServiceTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Solve_VerifiedArithmeticObligation_IsProven()
    {
        var router = new ScriptedAiRouter(new()
        {
            [IntelligencePromptCodes.MathProblemContract] = Serialize(new MathProblemContractProposal
            {
                Statement = "Show 2 + 2 = 4.",
                ProblemType = "PROVE",
                Domain = "MATHEMATICS",
                Target = "2 + 2 = 4",
                RequiresFormalVerification = true,
            }),
            [IntelligencePromptCodes.MathStrategyProposal] = Serialize(new MathStrategyProposal
            {
                Candidates =
                [
                    new MathStrategyCandidate { Name = "Direct computation", ObjectType = "PROOF_STRATEGY", Approach = "Evaluate", DiscoveryConfidence = 0.9 },
                    new MathStrategyCandidate { Name = "Counterexample search", ObjectType = "COUNTEREXAMPLE", Approach = "Seek violation", DiscoveryConfidence = 0.2 },
                ],
            }),
            [IntelligencePromptCodes.MathSolutionDerivation] = Serialize(new MathSolutionDerivationProposal
            {
                Nodes = [new MathProofNodeProposal { Id = "n1", NodeType = "CONCLUSION", Statement = "2 + 2 = 4" }],
                Obligations =
                [
                    new MathProofObligationProposal
                    {
                        Id = "o1",
                        Statement = "2 + 2 == 4",
                        VerificationMethod = "ALGEBRAIC",
                        NormalizedClaim = "2 + 2 == 4",
                        ExpectedRelation = "EQUAL",
                        Left = 4.0,
                        Right = 4.0,
                        DiscoveryConfidence = 0.9,
                    },
                ],
                Answer = new MathAnswerProposal { AnswerValue = "4", AnswerType = "NUMERIC", CanonicalForm = "4" },
            }),
            [IntelligencePromptCodes.MathAnswerExtraction] = Serialize(new MathAnswerProposal { AnswerValue = "4", AnswerType = "NUMERIC", CanonicalForm = "4" }),
            [IntelligencePromptCodes.MathAnswerComposer] = Serialize(new MathAnswerComposerProposal
            {
                Outcome = "PROVEN",
                FinalAnswer = "2 + 2 = 4",
                SolutionSummary = "Direct evaluation.",
                KeySteps = ["Evaluate 2 + 2"],
            }),
        });

        var service = new MathReasoningService(router, new PassThroughPromptCatalog(), NullLogger<MathReasoningService>.Instance);

        var response = await service.SolveAsync(new MathSolveRequest(Guid.NewGuid(), Guid.NewGuid(), "Show 2 + 2 = 4.", "corr-1"));

        Assert.Equal(ScientificOutcome.Proven.ToString(), response.Outcome);
        Assert.Equal(VerificationStatus.Verified.ToString(), response.VerificationStatus);
        Assert.Equal("4", response.CanonicalAnswer);
        var obligation = Assert.Single(response.Obligations);
        Assert.Equal(ObligationStatus.Verified.ToString(), obligation.Status);
    }

    [Fact]
    public async Task Solve_WithRepository_PersistsRunMirroringResponse()
    {
        var repository = new FakeMathReasoningRepository();
        var service = new MathReasoningService(BuildProvenRouter(), new PassThroughPromptCatalog(), NullLogger<MathReasoningService>.Instance, repository);

        var response = await service.SolveAsync(new MathSolveRequest(Guid.NewGuid(), Guid.NewGuid(), "Show 2 + 2 = 4.", "corr-persist"));

        var saved = Assert.Single(repository.Saved);
        Assert.Equal(response.CorrelationId, saved.CorrelationId);
        Assert.Equal(response.Outcome, saved.OutcomeCode);
        Assert.Equal(response.VerificationStatus, saved.VerificationStatusCode);
        Assert.Equal(response.CanonicalAnswer, saved.CanonicalAnswer);
        Assert.Equal(response.Obligations.Count, saved.Obligations.Count);
        Assert.NotNull(saved.DurationMilliseconds);
    }

    [Fact]
    public async Task Solve_RepositoryThrows_ResponseIsUnaffected()
    {
        var repository = new FakeMathReasoningRepository { ThrowOnSave = true };
        var service = new MathReasoningService(BuildProvenRouter(), new PassThroughPromptCatalog(), NullLogger<MathReasoningService>.Instance, repository);

        var response = await service.SolveAsync(new MathSolveRequest(Guid.NewGuid(), Guid.NewGuid(), "Show 2 + 2 = 4.", "corr-throw"));

        Assert.Equal(ScientificOutcome.Proven.ToString(), response.Outcome);
        Assert.Equal(VerificationStatus.Verified.ToString(), response.VerificationStatus);
    }

    private static ScriptedAiRouter BuildProvenRouter() => new(new()
    {
        [IntelligencePromptCodes.MathProblemContract] = Serialize(new MathProblemContractProposal
        {
            Statement = "Show 2 + 2 = 4.",
            ProblemType = "PROVE",
            Domain = "MATHEMATICS",
            Target = "2 + 2 = 4",
            RequiresFormalVerification = true,
        }),
        [IntelligencePromptCodes.MathStrategyProposal] = Serialize(new MathStrategyProposal
        {
            Candidates =
            [
                new MathStrategyCandidate { Name = "Direct computation", ObjectType = "PROOF_STRATEGY", Approach = "Evaluate", DiscoveryConfidence = 0.9 },
            ],
        }),
        [IntelligencePromptCodes.MathSolutionDerivation] = Serialize(new MathSolutionDerivationProposal
        {
            Nodes = [new MathProofNodeProposal { Id = "n1", NodeType = "CONCLUSION", Statement = "2 + 2 = 4" }],
            Obligations =
            [
                new MathProofObligationProposal
                {
                    Id = "o1",
                    Statement = "2 + 2 == 4",
                    VerificationMethod = "ALGEBRAIC",
                    NormalizedClaim = "2 + 2 == 4",
                    ExpectedRelation = "EQUAL",
                    Left = 4.0,
                    Right = 4.0,
                    DiscoveryConfidence = 0.9,
                },
            ],
            Answer = new MathAnswerProposal { AnswerValue = "4", AnswerType = "NUMERIC", CanonicalForm = "4" },
        }),
        [IntelligencePromptCodes.MathAnswerExtraction] = Serialize(new MathAnswerProposal { AnswerValue = "4", AnswerType = "NUMERIC", CanonicalForm = "4" }),
        [IntelligencePromptCodes.MathAnswerComposer] = Serialize(new MathAnswerComposerProposal
        {
            Outcome = "PROVEN",
            FinalAnswer = "2 + 2 = 4",
            SolutionSummary = "Direct evaluation.",
            KeySteps = ["Evaluate 2 + 2"],
        }),
    });

    [Fact]
    public async Task Solve_FailingArithmeticObligation_IsDisproven()
    {
        var router = new ScriptedAiRouter(new()
        {
            [IntelligencePromptCodes.MathProblemContract] = Serialize(new MathProblemContractProposal { Statement = "Claim 2 + 2 = 5.", ProblemType = "PROVE", Target = "2 + 2 = 5" }),
            [IntelligencePromptCodes.MathSolutionDerivation] = Serialize(new MathSolutionDerivationProposal
            {
                Nodes = [new MathProofNodeProposal { Id = "n1", NodeType = "CONCLUSION", Statement = "2 + 2 = 5" }],
                Obligations =
                [
                    new MathProofObligationProposal
                    {
                        Id = "o1",
                        Statement = "2 + 2 == 5",
                        VerificationMethod = "ALGEBRAIC",
                        NormalizedClaim = "2 + 2 == 5",
                        ExpectedRelation = "EQUAL",
                        Left = 4.0,
                        Right = 5.0,
                        DiscoveryConfidence = 0.95,
                    },
                ],
            }),
        });

        var service = new MathReasoningService(router, new PassThroughPromptCatalog(), NullLogger<MathReasoningService>.Instance);

        var response = await service.SolveAsync(new MathSolveRequest(Guid.NewGuid(), Guid.NewGuid(), "Claim 2 + 2 = 5.", "corr-2"));

        Assert.Equal(ScientificOutcome.Disproven.ToString(), response.Outcome);
        Assert.Equal(VerificationStatus.Refuted.ToString(), response.VerificationStatus);
    }

    [Fact]
    public async Task Solve_ProviderUnavailable_DegradesToInsufficientFormalization()
    {
        var service = new MathReasoningService(new UnavailableAiRouter(), new PassThroughPromptCatalog(), NullLogger<MathReasoningService>.Instance);

        var response = await service.SolveAsync(new MathSolveRequest(Guid.NewGuid(), Guid.NewGuid(), "Anything.", "corr-3"));

        Assert.Equal(ScientificOutcome.InsufficientFormalization.ToString(), response.Outcome);
        Assert.Empty(response.Obligations);
    }

    [Fact]
    public async Task Solve_HighDiscoveryButNoVerifiableObligation_IsNotProven()
    {
        var router = new ScriptedAiRouter(new()
        {
            [IntelligencePromptCodes.MathProblemContract] = Serialize(new MathProblemContractProposal { Statement = "Deep conjecture.", ProblemType = "PROVE", Target = "P" }),
            [IntelligencePromptCodes.MathSolutionDerivation] = Serialize(new MathSolutionDerivationProposal
            {
                Nodes = [new MathProofNodeProposal { Id = "n1", NodeType = "LEMMA", Statement = "hand-wave" }],
                Obligations =
                [
                    new MathProofObligationProposal { Id = "o1", Statement = "P holds", VerificationMethod = "LOGICAL", DiscoveryConfidence = 0.99 },
                ],
            }),
        });

        var service = new MathReasoningService(router, new PassThroughPromptCatalog(), NullLogger<MathReasoningService>.Instance);

        var response = await service.SolveAsync(new MathSolveRequest(Guid.NewGuid(), Guid.NewGuid(), "Deep conjecture.", "corr-4"));

        Assert.NotEqual(ScientificOutcome.Proven.ToString(), response.Outcome);
        Assert.NotEqual(VerificationStatus.Verified.ToString(), response.VerificationStatus);
    }

    [Fact]
    public void Mapper_ParsesUnknownEnums_ToSafeDefaults()
    {
        var contract = MathContractMapper.ToContract(new MathProblemContractProposal { ProblemType = "???", Domain = null }, "fallback");
        Assert.Equal(ScientificProblemType.Solve, contract.ProblemType);
        Assert.Equal(ScientificDomain.Mathematics, contract.Domain);
        Assert.Equal("fallback", contract.Statement);
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private sealed class PassThroughPromptCatalog : IPromptCatalog
    {
        public Task<string> GetSystemPromptAsync(Guid tenantId, string promptCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(promptCode);
    }

    private sealed class ScriptedAiRouter(Dictionary<string, string> byPromptCode) : IAiProviderRouter
    {
        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
        {
            // The orchestrator passes the prompt code as the system prompt (pass-through catalog) and as
            // executionContext.GroundingTitle; use whichever matches a scripted response, else empty.
            var key = byPromptCode.ContainsKey(systemPrompt) ? systemPrompt : executionContext?.GroundingTitle ?? string.Empty;
            var json = byPromptCode.TryGetValue(key, out var value) ? value : "{}";
            return Task.FromResult(new AiGenerationResult(json, json, 0, 0, null, "req", TimeSpan.Zero, "fake", "fake-model"));
        }

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

    private sealed class FakeMathReasoningRepository : IMathReasoningRepository
    {
        public List<MathExecutionRecord> Saved { get; } = [];

        public bool ThrowOnSave { get; init; }

        public Task<Guid> SaveMathExecutionAsync(MathExecutionRecord execution, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave)
                throw new InvalidOperationException("Simulated persistence failure.");

            Saved.Add(execution);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<IReadOnlyList<MathExecutionSummary>> ListMathExecutionsAsync(Guid tenantId, int take = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MathExecutionSummary>>([]);

        public Task<MathExecutionDetail?> GetMathExecutionAsync(Guid tenantId, Guid mathExecutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MathExecutionDetail?>(null);
    }
}