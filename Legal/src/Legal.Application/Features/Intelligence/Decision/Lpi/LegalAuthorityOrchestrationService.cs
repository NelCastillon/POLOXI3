using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LegalAuthorityOrchestrationService — drives the LegalAuthority channel into the SHARED LPI funnel.
//
// For each VERIFIED legal-authority evidence item (statute / regulation / case law in the matter
// evidence graph) it binds the authority summary to exactly one evidence-bearing node of the matter's
// AUTHORITATIVE persisted hierarchy using the SAME deterministic lexical overlap every channel uses
// (ChannelNodeTextMatcher — no LLM, no scoring engine), then PARKS a RetrievedProposition + placement
// for attorney review. It NEVER scores, applies, or ranks: scoring happens only when the attorney
// accepts the parked proposition through IPropositionIntegrationService.ApplyAsync, and POLOXI Core
// remains the sole competition authority.
//
// Invariants mirrored from the Document-Retrieval and Media-Evidence drivers:
//   * Only VERIFIED LEGAL_AUTHORITY evidence feeds a proposition (unverifiable citations are excluded).
//   * Exact source provenance is preserved (LegalDocumentVersionId + evidence-item source locator).
//   * A proposition that matched a node is parked ReviewRequired; an unmatched one is parked
//     NeedsHierarchyReview — ALWAYS preserved, never force-fit to a lexical near-miss.
//   * Idempotency keys derive from the stable evidence-item identity, so re-runs do not duplicate.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalAuthorityOrchestrationService(
    ILegalDocumentCorpusRepository documentCorpusRepository,
    ILegalHierarchyExecutionRepository hierarchyRepository,
    ILegalDecisionContractRepository decisionContractRepository,
    IDecisionRevisionResolver revisionResolver,
    ILpiPropositionIntegrationRepository integrationRepository,
    ILogger<LegalAuthorityOrchestrationService> logger) : ILegalAuthorityOrchestrationService
{
    private const int MinimumSharedTokens = 2;
    private const string LegalAuthorityEvidenceType = "LEGAL_AUTHORITY";

    public async Task<LegalAuthorityOrchestrationResult> RunAsync(
        LegalAuthorityOrchestrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1) Resolve the authoritative hierarchy execution for the matter (contract → authority → run).
        HierarchyExecutionDetailDto? execution;
        try
        {
            execution = await ResolveAuthoritativeExecutionAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load authoritative hierarchy for matter {Matter}.", request.MatterId);
            return new LegalAuthorityOrchestrationResult(0, 0, 0, "Failed", $"Hierarchy load failed: {ex.Message}");
        }

        if (execution is null || execution.Nodes.Count == 0)
            return new LegalAuthorityOrchestrationResult(0, 0, 0, "NoHierarchy",
                "No authoritative hierarchy is available for this matter; run a decision first.");

        // 2) Collect the VERIFIED legal-authority evidence to turn into parked propositions.
        LegalMatterEvidenceGraphDto evidenceGraph;
        try
        {
            evidenceGraph = await documentCorpusRepository.GetMatterEvidenceGraphAsync(
                request.TenantId, request.MatterId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load evidence graph for matter {Matter}.", request.MatterId);
            return new LegalAuthorityOrchestrationResult(0, 0, 0, "Failed", $"Evidence load failed: {ex.Message}");
        }

        var verifiedAuthorities = evidenceGraph.Evidence
            .Where(e => string.Equals(e.EvidenceTypeCode, LegalAuthorityEvidenceType, StringComparison.OrdinalIgnoreCase))
            .Where(e => string.Equals(e.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase))
            .Where(e => !string.IsNullOrWhiteSpace(e.Summary))
            .ToArray();
        if (verifiedAuthorities.Length == 0)
            return new LegalAuthorityOrchestrationResult(0, 0, 0, "NoAuthorities",
                "No VERIFIED legal-authority evidence is available to park for this matter.");

        // 3) Pre-tokenize the evidence-bearing nodes once; authority binds to a leaf proposition/factor/
        //    discriminator node, never a structural GROUPING/DIMENSION container.
        var nodeIndex = ChannelNodeTextMatcher.BuildNodeIndex(execution.Nodes);
        var hierarchyRevisionId = execution.Execution.HierarchyExecutionId;

        // 4) Resolve the server-side decision identity once for every parked proposition's context.
        var snapshot = await revisionResolver.ResolveAsync(request.TenantId, request.MatterId, cancellationToken);

        var scanned = 0;
        var matched = 0;
        var parked = 0;

        foreach (var authority in verifiedAuthorities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;

            var bestNode = nodeIndex.Count == 0
                ? null
                : ChannelNodeTextMatcher.BestMatch(authority.Summary, nodeIndex, MinimumSharedTokens, out var overlap) is { } node
                    ? (Node: node, Overlap: overlap)
                    : ((HierarchyNodeDto Node, int Overlap)?)null;

            var placements = new List<LpiPlacementProposal>();
            if (bestNode is { } hit)
            {
                matched++;
                placements.Add(new LpiPlacementProposal(
                    ProposalId: Guid.Empty,
                    HierarchyRevisionId: hierarchyRevisionId,
                    TargetNodeId: hit.Node.HierarchyNodeId,
                    LeftNeighborId: null,
                    RightNeighborId: null,
                    PlacementFraction: null,
                    Relationship: LpiRelationship.Supports,
                    Rationale: $"Verified legal authority ({authority.DimensionCode}) matched node on {hit.Overlap} shared terms."));
            }

            try
            {
                await ParkAuthorityAsync(request, authority, placements, snapshot, cancellationToken);
                parked++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to park legal authority {EvidenceItem} for matter {Matter}.",
                    authority.LegalEvidenceItemId, request.MatterId);
            }
        }

        return new LegalAuthorityOrchestrationResult(scanned, matched, parked, "Completed", null);
    }

    private async Task ParkAuthorityAsync(
        LegalAuthorityOrchestrationRequest request,
        LegalEvidenceGraphItemDto authority,
        IReadOnlyList<LpiPlacementProposal> placements,
        DecisionRevisionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var proposalId = Guid.NewGuid();

        // The source version id carries the originating legal-document version so provenance resolves back
        // to the exact authority text; the source locator points to the stable evidence-item identity.
        var proposition = new RetrievedProposition(
            proposalId, request.MatterId, authority.LegalDocumentVersionId,
            SourceLocator: $"legal-authority:{authority.LegalEvidenceItemId:N}",
            SourceText: authority.Summary,
            PropositionText: authority.Summary,
            AssertionType: LpiAssertionType.StatesLaw,
            AttributedTo: string.IsNullOrWhiteSpace(authority.DimensionCode) ? null : authority.DimensionCode,
            EffectiveAt: null);

        var reviewState = placements.Count == 0
            ? LpiProposalState.NeedsHierarchyReview.ToString()
            : LpiProposalState.ReviewRequired.ToString();
        var reviewReason = placements.Count == 0
            ? "Verified legal authority did not match an authoritative hierarchy node; attorney must place it."
            : "Verified legal authority parked for attorney review before integration.";

        var context = new LpiIntegrationContext(
            request.TenantId, request.ActorUserId, request.MatterId,
            snapshot.DecisionContractRevision, snapshot.CandidateSetRevision, snapshot.HierarchyRevision,
            SourceDocumentVersionId: authority.LegalDocumentVersionId, ReviewerUserId: request.ActorUserId,
            ScoringConfigurationVersion: snapshot.ScoringConfigurationVersion,
            IdempotencyKey: $"legal-authority-park:{authority.LegalEvidenceItemId:N}");

        await integrationRepository.ParkForReviewAsync(new LpiReviewPark(
            request.TenantId, request.ActorUserId, request.MatterId, LpiOperationKind.Add,
            proposition, placements, context, reviewState, reviewReason,
            RetrievalModeCode: nameof(LpiRetrievalMode.LegalAuthorityDirected)), cancellationToken);
    }

    private async Task<HierarchyExecutionDetailDto?> ResolveAuthoritativeExecutionAsync(
        LegalAuthorityOrchestrationRequest request, CancellationToken cancellationToken)
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

        return await hierarchyRepository.GetExecutionAsync(
            request.TenantId, authority.HierarchyExecutionId, cancellationToken);
    }
}
