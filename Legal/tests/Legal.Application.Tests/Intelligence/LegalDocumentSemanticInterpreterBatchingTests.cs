using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ──────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — CORPUS ACTIVATION INPUT-BUDGET BATCHING.
//
// The tenant AI safety guard (Intelligence.Safety.MaximumInputCharacters) rejects any single governed AI
// request whose systemPrompt+userPrompt exceeds the configured maximum. A large medical/expert PDF carries
// far more passage text than that ceiling, so sending all passages in one shot previously failed the whole
// document with "The AI request exceeded the configured maximum input length." and left the Evidence Graph
// empty. The interpreter now splits passages into batches that each stay under a safe input ceiling and
// merges the results, so oversized documents activate instead of failing.
//
// These tests prove:
//   • A document whose passage text exceeds the ceiling is sent as MULTIPLE requests, each under the limit.
//   • Every passage is still represented across the batches (none is silently dropped).
// ──────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDocumentSemanticInterpreterBatchingTests
{
    private const int MaximumInputCharacters = 58000;

    [Fact]
    public async Task InterpretAsync_SplitsOversizedDocumentIntoRequestsUnderSafetyLimit()
    {
        var passages = BuildPassages(count: 40, textLength: 4000);
        var router = new CapturingRouter();
        var interpreter = new LegalDocumentSemanticInterpreter(router);

        await interpreter.InterpretAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            domainPackCode: null,
            domainConcepts: [],
            passages: passages,
            correlationId: "batching",
            modelCodeOverride: null,
            resolvedPack: null);

        Assert.True(router.Requests.Count > 1, "Oversized document must be split into multiple requests.");
        Assert.All(router.Requests, sent =>
            Assert.True(sent.SystemPrompt.Length + sent.UserPrompt.Length <= MaximumInputCharacters,
                $"A request of {sent.SystemPrompt.Length + sent.UserPrompt.Length} chars exceeded the safety limit."));

        foreach (var passage in passages)
            Assert.Contains(router.Requests, sent => sent.UserPrompt.Contains(passage.LegalDocumentPassageId.ToString("D")));
    }

    [Fact]
    public async Task InterpretAsync_SendsSingleRequestForSmallDocument()
    {
        var passages = BuildPassages(count: 2, textLength: 500);
        var router = new CapturingRouter();
        var interpreter = new LegalDocumentSemanticInterpreter(router);

        await interpreter.InterpretAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            domainPackCode: null,
            domainConcepts: [],
            passages: passages,
            correlationId: "small",
            modelCodeOverride: null,
            resolvedPack: null);

        Assert.Single(router.Requests);
    }

    private static List<LegalDocumentPassageDto> BuildPassages(int count, int textLength)
        => Enumerable.Range(0, count)
            .Select(index => new LegalDocumentPassageDto(
                Guid.NewGuid(), Guid.NewGuid(), index + 1, $"/section/{index}", index,
                new string('x', textLength), LegalDocumentExtractionMethods.NativeText, 0.9m, null, null,
                LegalDocumentProcessingStates.Processed))
            .ToList();

    private sealed class CapturingRouter : IAiProviderRouter
    {
        public List<(string SystemPrompt, string UserPrompt)> Requests { get; } = [];

        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
        {
            Requests.Add((systemPrompt, userPrompt));
            return Task.FromResult(new AiGenerationResult("{}", "{}", 0, 0, null, "req", TimeSpan.Zero, "PROVIDER", "MODEL"));
        }

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
