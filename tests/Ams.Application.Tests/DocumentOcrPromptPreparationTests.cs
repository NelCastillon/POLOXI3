using System.Text.Json;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.DocumentIntake;
using Ams.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ams.Application.Tests;

public sealed class DocumentOcrPromptPreparationTests
{
    [Fact]
    public void PrepareChunks_RemovesOcrMetadataAndPreservesPageMarkers()
    {
        var json = JsonSerializer.Serialize(new
        {
            analyzeResult = new
            {
                content = "Insurance application page two",
                pages = new[]
                {
                    new
                    {
                        pageNumber = 2,
                        words = new[]
                        {
                            new { content = "Insurance", confidence = 0.99, polygon = new[] { 1, 2, 3, 4 } },
                            new { content = "application", confidence = 0.98, polygon = new[] { 5, 6, 7, 8 } }
                        }
                    }
                }
            }
        });

        var chunks = DocumentOcrPromptPreparer.PrepareChunks(json, 200);

        var chunk = Assert.Single(chunks);
        Assert.Contains("[Page 2]", chunk);
        Assert.Contains("Insurance application", chunk);
        Assert.DoesNotContain("confidence", chunk, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("polygon", chunk, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrepareChunks_SplitsOversizedPagesWithinBudget()
    {
        var json = JsonSerializer.Serialize(new
        {
            analyzeResult = new
            {
                content = string.Join(' ', Enumerable.Repeat("coverage", 100)),
                pages = new[]
                {
                    new
                    {
                        pageNumber = 1,
                        words = Enumerable.Repeat(new { content = "coverage", confidence = 0.95 }, 100).ToArray()
                    }
                }
            }
        });

        var chunks = DocumentOcrPromptPreparer.PrepareChunks(json, 120);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 120));
        Assert.All(chunks, chunk => Assert.Contains("[Page 1", chunk));
    }

    [Fact]
    public async Task InterpretAsync_AggregatesChunkClassificationsAndSendsCompactText()
    {
        var firstPage = string.Join(' ', Enumerable.Repeat("application", 10));
        var secondPage = string.Join(' ', Enumerable.Repeat("declarations", 10));
        var json = JsonSerializer.Serialize(new
        {
            analyzeResult = new
            {
                content = $"{firstPage} {secondPage}",
                pages = new object[]
                {
                    new { pageNumber = 1, words = firstPage.Split(' ').Select(x => new { content = x, confidence = 0.99, polygon = new[] { 1, 2 } }).ToArray() },
                    new { pageNumber = 2, words = secondPage.Split(' ').Select(x => new { content = x, confidence = 0.98, polygon = new[] { 3, 4 } }).ToArray() }
                }
            }
        });
        var router = new CapturingRouter(
            "{\"documentTypeCode\":\"ACORD_125\",\"confidence\":0.9}",
            "{\"documentTypeCode\":\"POLICY\",\"confidence\":0.8}");
        var provider = new AzureOpenAiDocumentInterpretationProvider(router, new SafetyPolicyRepository(300));
        var request = new DocumentInterpretationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SUBMISSION", "DOCUMENT.CLASSIFICATION", "1.0", "Classify.", "{}", json, "corr");

        var result = await provider.InterpretAsync(request);

        Assert.Equal(2, router.UserPrompts.Count);
        Assert.Equal("ACORD_125", result.Classification.DocumentTypeCode);
        Assert.InRange(result.Classification.Confidence, 0.4m, 0.5m);
        Assert.Equal(64, result.InputHashSha256.Length);
        Assert.All(router.UserPrompts, prompt =>
        {
            Assert.DoesNotContain("polygon", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.True(request.SystemPrompt.Length + prompt.Length <= 300);
        });
    }

    [Fact]
    public async Task InterpretAsync_MergesDuplicateExtractedFieldsUsingHighestConfidence()
    {
        var firstPage = string.Join(' ', Enumerable.Repeat("named insured", 8));
        var secondPage = string.Join(' ', Enumerable.Repeat("named insured", 8));
        var json = JsonSerializer.Serialize(new
        {
            analyzeResult = new
            {
                pages = new object[]
                {
                    new { pageNumber = 1, words = firstPage.Split(' ').Select(x => new { content = x }).ToArray() },
                    new { pageNumber = 2, words = secondPage.Split(' ').Select(x => new { content = x }).ToArray() }
                }
            }
        });
        var router = new CapturingRouter(
            "{\"fields\":[{\"entityTypeCode\":\"SUBMISSION\",\"entityKey\":\"root\",\"path\":\"submission.businessName\",\"value\":\"Contoso\",\"valueTypeCode\":\"STRING\",\"confidence\":0.7,\"sourcePage\":1,\"boundingBoxJson\":null}],\"warnings\":[]}",
            "{\"fields\":[{\"entityTypeCode\":\"SUBMISSION\",\"entityKey\":\"root\",\"path\":\"submission.businessName\",\"value\":\"Contoso LLC\",\"valueTypeCode\":\"STRING\",\"confidence\":0.95,\"sourcePage\":2,\"boundingBoxJson\":null}],\"warnings\":[]}");
        var provider = new AzureOpenAiDocumentInterpretationProvider(router, new SafetyPolicyRepository(260));
        var request = new DocumentInterpretationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SUBMISSION", "SUBMISSION.EXTRACTION", "2.0", "Extract.", "{}", json, "corr");

        var result = await provider.InterpretAsync(request);

        var field = Assert.Single(result.Fields);
        Assert.Equal("Contoso LLC", field.Value);
        Assert.Equal(0.95m, field.Confidence);
        Assert.Equal(2, field.SourcePage);
    }

    [Fact]
    public async Task InterpretAsync_Routes333099CharacterOcrPayloadWithoutExceedingSafetyLimit()
    {
        const int maximumInputCharacters = 20_000;
        const int rawPayloadCharacters = 333_099;
        var words = Enumerable.Range(0, 1_600)
            .Select(index => new { content = $"risk{index}", confidence = 0.99, polygon = new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8 } })
            .ToArray();
        var payload = new
        {
            analyzeResult = new
            {
                content = string.Join(' ', words.Select(x => x.content)),
                pages = new[] { new { pageNumber = 1, words } }
            },
            padding = string.Empty
        };
        var json = JsonSerializer.Serialize(payload);
        Assert.True(json.Length < rawPayloadCharacters);
        json = JsonSerializer.Serialize(new
        {
            payload.analyzeResult,
            padding = new string('x', rawPayloadCharacters - json.Length)
        });
        Assert.Equal(rawPayloadCharacters, json.Length);

        var repository = new SafetyPolicyRepository(maximumInputCharacters);
        var aiProvider = new SuccessfulAiProvider();
        var router = new AiProviderRouter(repository, [aiProvider], NullLogger<AiProviderRouter>.Instance);
        var provider = new AzureOpenAiDocumentInterpretationProvider(router, repository);
        var request = new DocumentInterpretationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SUBMISSION", "DOCUMENT.CLASSIFICATION", "1.0", "Classify the insurance document.", "{}", json, "large-ocr");

        var result = await provider.InterpretAsync(request);

        Assert.Equal("ACORD_125", result.Classification.DocumentTypeCode);
        Assert.NotEmpty(aiProvider.Requests);
        Assert.All(aiProvider.Requests, sent => Assert.InRange(sent.SystemPrompt.Length + sent.UserPrompt.Length, 1, maximumInputCharacters));
        Assert.All(aiProvider.Requests, sent => Assert.DoesNotContain("polygon", sent.UserPrompt, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingRouter(params string[] outputs) : IAiProviderRouter
    {
        private int _index;
        public List<string> UserPrompts { get; } = [];

        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
        {
            UserPrompts.Add(userPrompt);
            var output = outputs[Math.Min(_index++, outputs.Length - 1)];
            return Task.FromResult(new AiGenerationResult(output, output, 10, 5, null, Guid.NewGuid().ToString("N"), TimeSpan.FromMilliseconds(5), "TEST", "TEST_MODEL"));
        }

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class SafetyPolicyRepository(int maximumInputCharacters) : IAiProviderRouteRepository
    {
        public Task<AiSafetyPolicy> GetSafetyPolicyAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiSafetyPolicy(maximumInputCharacters, 20_000, []));

        public Task<IReadOnlyCollection<AiProviderRoute>> GetRoutesAsync(Guid tenantId, string featureCode, string capabilityCode, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<AiProviderRoute>>([new(tenantId, featureCode, "TEST", "TEST", "TEST_MODEL", "test", null, null, null, 30, 0, 1_000, 0, false)]);

        public Task RecordSafetyEventAsync(AiSafetyEventRecord safetyEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RecordExecutionAsync(AiExecutionRecord execution, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class SuccessfulAiProvider : IAiProvider
    {
        public string ProviderTypeCode => "TEST";
        public List<AiGenerationRequest> Requests { get; } = [];

        public Task<AiProviderHealth> CheckHealthAsync(AiProviderContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiProviderHealth("HEALTHY", "Ready", TimeSpan.Zero));

        public Task<AiGenerationResult> GenerateAsync(AiGenerationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            const string output = "{\"documentTypeCode\":\"ACORD_125\",\"confidence\":0.95}";
            return Task.FromResult(new AiGenerationResult(output, output, 100, 10, 0.95m, Guid.NewGuid().ToString("N"), TimeSpan.FromMilliseconds(1), "TEST", "TEST_MODEL"));
        }

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(AiEmbeddingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
