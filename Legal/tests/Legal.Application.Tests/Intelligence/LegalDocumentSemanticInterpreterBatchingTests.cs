using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using System.Text.Json;
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

    [Fact]
    public async Task InterpretAsync_BindsUnambiguousTypeAndPreservesSupportedFact()
    {
        var passage = Assert.Single(BuildPassages(1, 100));
        var concepts = new[]
        {
            Concept("DAMAGES_QUANTUM", "DAMAGES", 10),
            Concept("MEDICAL_LIEN_SUBROGATION", "DAMAGES", 20)
        };
        var router = new CapturingRouter
        {
            Output = JsonSerializer.Serialize(new LegalDocumentSemanticProposal(null, null,
                [new LegalEvidenceSemanticProposal("E1", passage.LegalDocumentPassageId, "MEDICAL_LIEN", "DAMAGES", "Lien is asserted.", .9m, "UNKNOWN", null)],
                [new LegalFactSemanticProposal("F1", "Lien is asserted.", LegalFactStates.Alleged, .9m)],
                [new LegalSemanticRelationshipProposal("E1", "F1", LegalDocumentRelationshipTypes.Supports, null)], [], []))
        };
        var pack = new ResolvedDomainPack("PERSONAL_INJURY", "PERSONAL_INJURY", concepts, [], [], [], [])
        {
            EvidenceTypes = [new DecisionDomainPackEvidenceTypeDto("MEDICAL_LIEN", "Medical lien", "DAMAGES", null)]
        };

        var proposal = await new LegalDocumentSemanticInterpreter(router).InterpretAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            pack.PackCode, concepts, [passage], "binding", resolvedPack: pack);

        Assert.Equal("MEDICAL_LIEN_SUBROGATION", Assert.Single(proposal.EvidenceItems).DomainConceptCode);
        Assert.Single(proposal.FactPropositions);
    }

    [Fact]
    public async Task InterpretAsync_DoesNotAdmitAmbiguousConceptOrUnlinkedFact()
    {
        var passage = Assert.Single(BuildPassages(1, 100));
        var concepts = new[] { Concept("DAMAGES_QUANTUM", "DAMAGES", 10), Concept("MEDICAL_LIEN_SUBROGATION", "DAMAGES", 20) };
        var router = new CapturingRouter
        {
            Output = JsonSerializer.Serialize(new LegalDocumentSemanticProposal(null, null,
                [new LegalEvidenceSemanticProposal("E1", passage.LegalDocumentPassageId, "UNCLASSIFIED", "DAMAGES", "Unknown damage.", .9m, null, null)],
                [new LegalFactSemanticProposal("F1", "Unknown damage.", LegalFactStates.Alleged, .9m)],
                [new LegalSemanticRelationshipProposal("E1", "F1", LegalDocumentRelationshipTypes.Supports, null)], [], []))
        };
        var pack = new ResolvedDomainPack("PERSONAL_INJURY", "PERSONAL_INJURY", concepts, [], [], [], []);

        var proposal = await new LegalDocumentSemanticInterpreter(router).InterpretAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            pack.PackCode, concepts, [passage], "ambiguous", resolvedPack: pack);

        Assert.Empty(proposal.EvidenceItems);
        Assert.Empty(proposal.FactPropositions);
    }

    [Fact]
    public async Task InterpretAsync_BindsInsurancePolicyToCanonicalCoverageDimension()
    {
        var passage = Assert.Single(BuildPassages(1, 120));
        var concepts = new[]
        {
            Concept("INSURANCE_COVERAGE_LIMITS", "INSURANCE", 80),
            Concept("INSURANCE_POLICY_COVERAGE", "INSURANCE_COVERAGE", 611),
            Concept("LIABILITY_DUTY_BREACH", "LIABILITY", 10)
        };
        var router = new CapturingRouter
        {
            Output = JsonSerializer.Serialize(new LegalDocumentSemanticProposal("INSURANCE_POLICY", .9m,
                [new LegalEvidenceSemanticProposal("E1", passage.LegalDocumentPassageId, "INSURANCE_POLICY", "INSURANCE_COVERAGE", "Policy lists a bodily injury limit.", .9m, "UNKNOWN_POLICY", null)],
                [new LegalFactSemanticProposal("F1", "The policy states a bodily injury limit.", LegalFactStates.Alleged, .9m)],
                [new LegalSemanticRelationshipProposal("E1", "F1", LegalDocumentRelationshipTypes.Supports, null)], [], []))
        };
        var pack = new ResolvedDomainPack("PERSONAL_INJURY", "PERSONAL_INJURY", concepts, [], [], [], [])
        {
            EvidenceTypes = [new DecisionDomainPackEvidenceTypeDto("INSURANCE_POLICY", "Insurance Policy", "INSURANCE_COVERAGE", null)]
        };

        var proposal = await new LegalDocumentSemanticInterpreter(router).InterpretAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            pack.PackCode, concepts, [passage], "insurance", resolvedPack: pack);

        var evidence = Assert.Single(proposal.EvidenceItems);
        Assert.Equal("INSURANCE_POLICY_COVERAGE", evidence.DomainConceptCode);
        Assert.Equal("INSURANCE_COVERAGE", evidence.DimensionCode);
        Assert.Single(proposal.FactPropositions);
    }

    private static DecisionDomainConceptDto Concept(string code, string dimension, int order)
        => new(Guid.NewGuid(), code, dimension, code, null, "MIXED", "MATTER_DOCUMENT", null, null, null, false, true, order, 1);

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
        public string Output { get; init; } = "{}";

        public Task<AiGenerationResult> GenerateAsync(Guid tenantId, string featureCode, string systemPrompt, string userPrompt, string? outputSchemaJson, string correlationId, AiExecutionContext? executionContext = null, string? modelCodeOverride = null, CancellationToken cancellationToken = default)
        {
            Requests.Add((systemPrompt, userPrompt));
            return Task.FromResult(new AiGenerationResult(Output, Output, 0, 0, null, "req", TimeSpan.Zero, "PROVIDER", "MODEL"));
        }

        public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId, string featureCode, IReadOnlyCollection<string> inputs, string correlationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
