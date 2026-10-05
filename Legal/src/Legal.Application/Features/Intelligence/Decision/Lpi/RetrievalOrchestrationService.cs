using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// RetrievalOrchestrationService — drives the Document-Retrieval fan-out (Phase 2).
//
// Loads the authoritative hierarchy once (→ {{CONTEXT}} JSON + node resolver), selects passages for the
// requested retrieval mode, and feeds each one through the shared extraction→park seam
// (IRetrievalPropositionExtractionService). Every extracted proposition is parked for attorney review.
// The orchestrator NEVER scores, applies, or ranks — POLOXI Core remains the sole competition authority
// and acceptance flows through the shared IPropositionIntegrationService funnel.
//
//   * ConditionDirected — relevance-select passages via SearchRoutedMatterContextAsync for the open
//                         conditions. Relevance SELECTS passages; it never assigns outcome support.
//   * DocumentDirected  — scan matter document passages (optionally scoped to given versions) for
//                         material information that may not fit the current hierarchy.
//
// Fail-soft: a missing authoritative hierarchy or no passages returns a terminal status without
// throwing; a per-passage extraction failure is recorded and the pass continues.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class RetrievalOrchestrationService(
    ILegalHierarchyExecutionRepository hierarchyRepository,
    ILegalDecisionContractRepository decisionContractRepository,
    ILegalDocumentCorpusRepository documentCorpusRepository,
    IRetrievalPropositionExtractionService extractionService,
    ILogger<RetrievalOrchestrationService> logger) : IRetrievalOrchestrationService
{
    public async Task<RetrievalOrchestrationResult> RunAsync(
        RetrievalOrchestrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1) Load the authoritative hierarchy nodes once for the {{CONTEXT}} JSON + node resolver.
        LpiHierarchyContext? context;
        try
        {
            context = await BuildContextAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load hierarchy context for matter {Matter}.", request.MatterId);
            return new RetrievalOrchestrationResult(0, 0, 0, [], "Failed", $"Hierarchy context load failed: {ex.Message}");
        }

        if (context is null)
            return new RetrievalOrchestrationResult(0, 0, 0, [], "NoHierarchy",
                "No authoritative hierarchy is available for this matter; run a decision first.");

        // 2) Select passages for the requested retrieval mode.
        IReadOnlyList<RetrievalPassageSource> passages;
        try
        {
            passages = request.Mode == LpiRetrievalMode.ConditionDirected
                ? await SelectConditionDirectedAsync(request, cancellationToken)
                : await SelectDocumentDirectedAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Passage selection failed for matter {Matter}.", request.MatterId);
            return new RetrievalOrchestrationResult(0, 0, 0, [], "Failed", $"Passage selection failed: {ex.Message}");
        }

        if (passages.Count == 0)
            return new RetrievalOrchestrationResult(0, 0, 0, [], "NoPassages",
                "No passages matched this retrieval pass.");

        // 3) Feed each passage through the shared extraction→park seam and aggregate.
        var results = new List<RetrievalOrchestrationPassage>(passages.Count);
        var totalExtracted = 0;
        var totalParked = 0;

        foreach (var passage in passages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractionRequest = new RetrievalExtractionRequest(
                request.TenantId,
                request.ActorUserId,
                request.MatterId,
                passage.DocumentVersionId,
                request.DecisionContractRevision,
                request.CandidateSetRevision,
                request.HierarchyRevision,
                request.ScoringConfigurationVersion,
                request.CorrelationId,
                request.DecisionQuestion,
                context.ContextJson,
                passage.Text,
                passage.SourceLocator);

            var extraction = await extractionService.ExtractAndParkAsync(extractionRequest, cancellationToken);

            totalExtracted += extraction.PropositionsExtracted;
            totalParked += extraction.ItemsParked;
            results.Add(new RetrievalOrchestrationPassage(
                passage.DocumentVersionId,
                passage.PassageId,
                passage.SourceLocator,
                extraction.PropositionsExtracted,
                extraction.ItemsParked,
                extraction.StatusCode,
                extraction.FailureReason));
        }

        return new RetrievalOrchestrationResult(
            passages.Count, totalExtracted, totalParked, results, "Completed", null);
    }

    private async Task<LpiHierarchyContext?> BuildContextAsync(
        RetrievalOrchestrationRequest request, CancellationToken cancellationToken)
    {
        var contract = await decisionContractRepository.GetCurrentContractAsync(
            request.TenantId, request.MatterId, cancellationToken);
        if (contract is null)
            return null;

        var authority = await hierarchyRepository.GetCurrentAuthorityAsync(
            request.TenantId, request.MatterId, contract.DecisionContractId, contract.VersionNumber,
            cancellationToken);
        if (authority is null)
            return null;

        var execution = await hierarchyRepository.GetExecutionAsync(
            request.TenantId, authority.HierarchyExecutionId, cancellationToken);
        if (execution is null || execution.Nodes.Count == 0)
            return null;

        return LpiHierarchyContextBuilder.Build(request.DecisionQuestion, execution.Nodes);
    }

    private async Task<IReadOnlyList<RetrievalPassageSource>> SelectConditionDirectedAsync(
        RetrievalOrchestrationRequest request, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(request.RetrievalQuery)
            ? request.DecisionQuestion
            : request.RetrievalQuery;

        var items = await documentCorpusRepository.SearchRoutedMatterContextAsync(
            request.TenantId, request.MatterId, query, [], request.MaxPassages,
            cancellationToken: cancellationToken);

        return items
            .Where(i => i.LegalDocumentVersionId is not null && !string.IsNullOrWhiteSpace(i.Text))
            .Take(request.MaxPassages)
            .Select(i => new RetrievalPassageSource(
                i.LegalDocumentVersionId!.Value,
                i.PassageId,
                BuildLocator(i.SourceReference, i.PageNumber, i.PassageId),
                i.Text))
            .ToList();
    }

    private async Task<IReadOnlyList<RetrievalPassageSource>> SelectDocumentDirectedAsync(
        RetrievalOrchestrationRequest request, CancellationToken cancellationToken)
    {
        var versionFilter = request.DocumentVersionIds is { Count: > 0 }
            ? new HashSet<Guid>(request.DocumentVersionIds)
            : null;

        IReadOnlyCollection<Guid> versionIds;
        if (versionFilter is not null)
        {
            versionIds = versionFilter;
        }
        else
        {
            var documents = await documentCorpusRepository.GetMatterDocumentsAsync(
                request.TenantId, request.MatterId, cancellationToken);
            versionIds = documents
                .SelectMany(d => d.Versions.Select(v => v.LegalDocumentVersionId))
                .Distinct()
                .ToList();
        }

        var sources = new List<RetrievalPassageSource>();
        foreach (var versionId in versionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sources.Count >= request.MaxPassages)
                break;

            var passages = await documentCorpusRepository.GetDocumentPassagesAsync(
                request.TenantId, versionId, cancellationToken);

            foreach (var passage in passages)
            {
                if (sources.Count >= request.MaxPassages)
                    break;
                if (string.IsNullOrWhiteSpace(passage.Text))
                    continue;

                sources.Add(new RetrievalPassageSource(
                    passage.LegalDocumentVersionId,
                    passage.LegalDocumentPassageId,
                    BuildLocator(passage.SectionPath, passage.PageNumber, passage.LegalDocumentPassageId),
                    passage.Text));
            }
        }

        return sources;
    }

    private static string BuildLocator(string? reference, int? pageNumber, Guid? passageId)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(reference))
            parts.Add(reference!.Trim());
        if (pageNumber is int page)
            parts.Add($"p.{page}");
        if (passageId is Guid id)
            parts.Add($"passage:{id}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "unspecified";
    }

    private sealed record RetrievalPassageSource(
        Guid DocumentVersionId,
        Guid? PassageId,
        string SourceLocator,
        string Text);
}
