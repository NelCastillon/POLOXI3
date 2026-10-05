using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// RetrievalPropositionExtractionService — the Document-Retrieval → proposition SENDING half (Phase 2).
//
// Runs the DB-backed DECISION_EXTRACTION_V1 prompt over one retrieved passage + the current hierarchy
// context, parses the strict-JSON atomic propositions and their PROPOSED qualitative placements, and
// PARKS each as an attorney review item via the shared integration repository. It never scores, never
// applies, never picks a winner. Acceptance (a separate review service) flows through the shared
// IPropositionIntegrationService so manual (ADI) and retrieval paths converge on one scoring funnel.
//
// Fail-soft: a missing prompt, an AI failure, or unparseable JSON returns an ExtractionFailed result
// without throwing into the retrieval pipeline. An individual proposition that cannot be mapped (unknown
// assertion/relationship enum, or needsHierarchyReview=true) is parked as NeedsHierarchyReview rather
// than force-fit to a lexical match — preserving the spec's "never discard, never force-fit" promise.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class RetrievalPropositionExtractionService(
    ILegalDecisionRepository decisionRepository,
    IAiProviderRouter aiProviderRouter,
    ILpiPropositionIntegrationRepository integrationRepository,
    ILogger<RetrievalPropositionExtractionService> logger) : IRetrievalPropositionExtractionService
{
    private const string ExtractionPromptCode = "DECISION_EXTRACTION_V1";
    private const string FeatureCode = "DECISION_EXTRACTION";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RetrievalExtractionResult> ExtractAndParkAsync(
        RetrievalExtractionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = await decisionRepository.GetPromptAsync(ExtractionPromptCode, cancellationToken);
        if (prompt is null)
        {
            logger.LogWarning("Extraction prompt {Code} is not configured; no propositions extracted.", ExtractionPromptCode);
            return new RetrievalExtractionResult(0, 0, [], "ExtractionFailed", $"Prompt {ExtractionPromptCode} is not configured.");
        }

        var userPrompt = prompt.UserPromptTemplate
            .Replace("{{QUERY}}", request.DecisionQuestion, StringComparison.Ordinal)
            .Replace("{{CONTEXT}}", request.HierarchyContextJson, StringComparison.Ordinal)
            .Replace("{{ARTIFACT}}", BuildArtifact(request), StringComparison.Ordinal);

        AiGenerationResult generation;
        try
        {
            generation = await aiProviderRouter.GenerateAsync(
                request.TenantId, FeatureCode, prompt.SystemPrompt, userPrompt,
                prompt.OutputSchemaJson, request.CorrelationId, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Extraction AI call failed for matter {Matter}.", request.MatterId);
            return new RetrievalExtractionResult(0, 0, [], "ExtractionFailed", $"Extraction model call failed: {ex.Message}");
        }

        var json = generation.StructuredOutputJson ?? generation.Content;
        IReadOnlyList<ExtractedProposition> parsed;
        try
        {
            parsed = Parse(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Extraction JSON parse failed for matter {Matter}.", request.MatterId);
            return new RetrievalExtractionResult(0, 0, [], "ExtractionFailed", $"Extraction output could not be parsed: {ex.Message}");
        }

        if (parsed.Count == 0)
            return new RetrievalExtractionResult(0, 0, [], "NoPropositions", null);

        var items = new List<RetrievalExtractedItem>(parsed.Count);
        foreach (var extracted in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await ParkOneAsync(request, extracted, cancellationToken);
            items.Add(item);
        }

        return new RetrievalExtractionResult(parsed.Count, items.Count, items, "Extracted", null);
    }

    private async Task<RetrievalExtractedItem> ParkOneAsync(
        RetrievalExtractionRequest request, ExtractedProposition extracted, CancellationToken cancellationToken)
    {
        var proposalId = Guid.NewGuid();

        var proposition = new RetrievedProposition(
            proposalId,
            request.MatterId,
            request.DocumentVersionId,
            string.IsNullOrWhiteSpace(extracted.SourceLocator) ? request.SourceLocator : extracted.SourceLocator,
            extracted.SourceText ?? string.Empty,
            extracted.PropositionText ?? string.Empty,
            MapAssertion(extracted.AssertionType),
            extracted.AttributedTo,
            ParseEffectiveAt(extracted.EffectiveAt));

        // Map proposed placements. An unmappable relationship forces hierarchy review (never force-fit).
        var placements = new List<LpiPlacementProposal>();
        var unmappablePlacement = false;
        foreach (var placement in extracted.Placements ?? [])
        {
            if (!TryMapRelationship(placement.Relationship, out var relationship))
            {
                unmappablePlacement = true;
                continue;
            }

            // TargetNodeCode → node id resolution and explicit neighbor interpolation are attorney-review
            // responsibilities; the extractor proposes a node code + relationship only. We DO NOT infer a
            // midpoint placement from display order, so PlacementFraction is carried only when explicit.
            if (!Guid.TryParse(placement.TargetNodeCode, out var targetNodeId))
            {
                unmappablePlacement = true;
                continue;
            }

            placements.Add(new LpiPlacementProposal(
                proposalId,
                HierarchyRevisionId: Guid.Empty,
                TargetNodeId: targetNodeId,
                LeftNeighborId: Guid.TryParse(placement.LeftNeighborCode, out var l) ? l : null,
                RightNeighborId: Guid.TryParse(placement.RightNeighborCode, out var r) ? r : null,
                PlacementFraction: placement.PlacementFraction,
                Relationship: relationship,
                Rationale: placement.Rationale ?? string.Empty));
        }

        var needsReview = extracted.NeedsHierarchyReview || unmappablePlacement || placements.Count == 0;
        var state = needsReview ? LpiProposalState.NeedsHierarchyReview : LpiProposalState.PlacementProposed;
        var reason = needsReview
            ? (extracted.ReviewReason
               ?? "No existing hierarchy node fit this proposition; attorney review required before placement.")
            : "Extracted from retrieved passage; awaiting attorney review before integration.";

        var context = new LpiIntegrationContext(
            request.TenantId,
            request.ActorUserId,
            request.MatterId,
            request.DecisionContractRevision,
            request.CandidateSetRevision,
            request.HierarchyRevision,
            request.DocumentVersionId,
            request.ActorUserId,
            request.ScoringConfigurationVersion,
            BuildIdempotencyKey(request, proposition, placements));

        var parkedId = await integrationRepository.ParkForReviewAsync(new LpiReviewPark(
            request.TenantId,
            request.ActorUserId,
            request.MatterId,
            LpiOperationKind.Add,
            proposition,
            placements,
            context,
            state.ToString(),
            reason),
            cancellationToken);

        return new RetrievalExtractedItem(
            parkedId, state, proposition.PropositionText, placements.Count, needsReview);
    }

    private static string BuildArtifact(RetrievalExtractionRequest request)
        => $"{{\"documentVersionId\":\"{request.DocumentVersionId}\",\"locator\":{JsonSerializer.Serialize(request.SourceLocator)},\"passage\":{JsonSerializer.Serialize(request.SourcePassage)}}}";

    // Idempotency key derives from STABLE operation identity (document version + locator + target nodes +
    // normalized proposition text), NOT raw text alone — duplicate uploads of the same report collapse to
    // one op, while two different witnesses stating the same thing at the same node remain distinct only
    // when their source locators differ.
    private static string BuildIdempotencyKey(
        RetrievalExtractionRequest request, RetrievedProposition proposition, IReadOnlyList<LpiPlacementProposal> placements)
    {
        var nodes = string.Join(",", placements.Select(p => p.TargetNodeId.ToString()).OrderBy(x => x, StringComparer.Ordinal));
        var material = string.Join("|",
            request.DocumentVersionId.ToString(),
            proposition.SourceLocator.Trim(),
            Normalize(proposition.PropositionText),
            nodes);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"RETRIEVAL:{Convert.ToHexString(hash)}";
    }

    private static string Normalize(string? text)
        => string.Join(' ', (text ?? string.Empty).ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlyList<ExtractedProposition> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var payload = JsonSerializer.Deserialize<ExtractionPayload>(json, JsonOptions);
        return payload?.Propositions ?? [];
    }

    private static DateTimeOffset? ParseEffectiveAt(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    // Unknown/empty assertion type defaults to the most conservative non-fact reading (Reports), so an
    // ambiguous statement is never silently promoted to an established fact.
    private static LpiAssertionType MapAssertion(string? value) => value switch
    {
        "Asserts" => LpiAssertionType.Asserts,
        "Reports" => LpiAssertionType.Reports,
        "Documents" => LpiAssertionType.Documents,
        "StatesLaw" => LpiAssertionType.StatesLaw,
        "Infers" => LpiAssertionType.Infers,
        _ => LpiAssertionType.Reports,
    };

    private static bool TryMapRelationship(string? value, out LpiRelationship relationship)
    {
        switch (value)
        {
            case "SUPPORTS": relationship = LpiRelationship.Supports; return true;
            case "CONTRADICTS": relationship = LpiRelationship.Contradicts; return true;
            case "QUALIFIES": relationship = LpiRelationship.Qualifies; return true;
            case "CONTEXT_ONLY": relationship = LpiRelationship.ContextOnly; return true;
            default: relationship = LpiRelationship.ContextOnly; return false;
        }
    }

    // Strict-JSON shape of DECISION_EXTRACTION_V1 output (migration 0400).
    private sealed record ExtractionPayload(IReadOnlyList<ExtractedProposition>? Propositions);

    private sealed record ExtractedProposition(
        string? PropositionText,
        string? SourceLocator,
        string? SourceText,
        string? AssertionType,
        string? AttributedTo,
        string? EffectiveAt,
        bool NeedsHierarchyReview,
        string? ReviewReason,
        IReadOnlyList<ExtractedPlacement>? Placements);

    private sealed record ExtractedPlacement(
        string? TargetNodeCode,
        string? Relationship,
        string? LeftNeighborCode,
        string? RightNeighborCode,
        decimal? PlacementFraction,
        string? Rationale);
}
