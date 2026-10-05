using System.Security.Cryptography;
using System.Text;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// RetrievalPropositionReviewService — the acceptance half of the Document-Retrieval pipeline.
//
// Lists parked review items and, on attorney accept, reconstructs the RetrievedProposition + accepted
// placements from persistence, builds the LpiIntegrationContext (revisions, reviewer, scoring-config,
// stable idempotency key), and routes them through the SHARED IPropositionIntegrationService.ApplyAsync
// so the retrieval path and the manual ADI path converge on one insertion funnel and one reassessment.
// Reject only updates state — the proposition is preserved, never discarded.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class RetrievalPropositionReviewService(
    ILpiPropositionIntegrationRepository repository,
    IPropositionIntegrationService integrationService) : IRetrievalPropositionReviewService
{
    public async Task<IReadOnlyList<LpiReviewItemView>> GetPendingAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        var items = await repository.GetPendingReviewItemsAsync(tenantId, decisionMatterId, cancellationToken);
        return items.Select(ToView).ToList();
    }

    public async Task<LpiIntegrationResult> AcceptAsync(
        LpiReviewAcceptRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = await repository.GetReviewItemAsync(request.TenantId, request.RetrievedPropositionId, cancellationToken);
        if (item is null)
        {
            return new LpiIntegrationResult(
                false, null, [], false, "Rejected",
                "The proposition no longer exists or is not visible to this tenant.");
        }

        if (!IsReviewable(item.StateCode))
        {
            return new LpiIntegrationResult(
                false, null, [], false, "Rejected",
                $"The proposition is in a non-reviewable state ({item.StateCode}).");
        }

        // Select the accepted placements (optionally narrowed to a reviewed subset). CONTEXT_ONLY links
        // are carried through unchanged — the funnel is responsible for giving them no numeric support.
        var requestedNodes = request.AcceptedPlacementTargetNodeIds is { Count: > 0 } subset
            ? new HashSet<Guid>(subset)
            : null;

        var placements = item.Placements
            .Where(p => requestedNodes is null || requestedNodes.Contains(p.TargetNodeId))
            .Select(p => new LpiPlacementProposal(
                item.RetrievedPropositionId,
                HierarchyRevisionId: Guid.Empty,
                TargetNodeId: p.TargetNodeId,
                LeftNeighborId: p.LeftNeighborId,
                RightNeighborId: p.RightNeighborId,
                PlacementFraction: p.PlacementFraction,
                Relationship: MapRelationship(p.RelationshipCode),
                Rationale: p.Rationale))
            .ToList();

        if (placements.Count == 0)
        {
            return new LpiIntegrationResult(
                false, null, [], false, "Rejected",
                "No accepted placement was supplied; nothing can be integrated.");
        }

        var proposition = new RetrievedProposition(
            item.RetrievedPropositionId,
            item.DecisionMatterId,
            item.DocumentVersionId,
            item.SourceLocator,
            item.SourceText,
            item.PropositionText,
            MapAssertion(item.AssertionTypeCode),
            item.AttributedTo,
            item.EffectiveAt);

        var context = new LpiIntegrationContext(
            request.TenantId,
            request.ReviewerUserId,
            item.DecisionMatterId,
            request.DecisionContractRevision,
            request.CandidateSetRevision,
            request.HierarchyRevision,
            item.DocumentVersionId,
            request.ReviewerUserId,
            request.ScoringConfigurationVersion,
            BuildIdempotencyKey(item, placements));

        return await integrationService.ApplyAsync(proposition, placements, context, LpiOperationKind.Add, cancellationToken, item.RetrievalModeCode);
    }

    public async Task<LpiIntegrationResult> ReviseAsync(
        LpiReviewReviseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prior = await repository.GetAcceptedPropositionAsync(
            request.TenantId, request.SupersedesPropositionId, cancellationToken);
        if (prior is null)
        {
            return new LpiIntegrationResult(
                false, null, [], false, "Rejected",
                "The proposition being revised no longer exists in an accepted state for this tenant.");
        }

        // Corrected placements: explicit edits when supplied, otherwise reuse the prior accepted placements.
        var placements = request.Placements is { Count: > 0 } edits
            ? edits.Select(e => new LpiPlacementProposal(
                prior.RetrievedPropositionId,
                HierarchyRevisionId: Guid.Empty,
                TargetNodeId: e.TargetNodeId,
                LeftNeighborId: e.LeftNeighborId,
                RightNeighborId: e.RightNeighborId,
                PlacementFraction: e.PlacementFraction,
                Relationship: MapRelationship(e.RelationshipCode),
                Rationale: e.Rationale)).ToList()
            : prior.Placements.Select(p => new LpiPlacementProposal(
                prior.RetrievedPropositionId,
                HierarchyRevisionId: Guid.Empty,
                TargetNodeId: p.TargetNodeId,
                LeftNeighborId: p.LeftNeighborId,
                RightNeighborId: p.RightNeighborId,
                PlacementFraction: p.PlacementFraction,
                Relationship: MapRelationship(p.RelationshipCode),
                Rationale: p.Rationale)).ToList();

        if (placements.Count == 0)
        {
            return new LpiIntegrationResult(
                false, null, [], false, "Rejected",
                "No placement was supplied for the revision; nothing can be integrated.");
        }

        // The corrected proposition carries the SUPERSEDED proposition's id so the funnel supersedes it.
        var proposition = new RetrievedProposition(
            prior.RetrievedPropositionId,
            prior.DecisionMatterId,
            prior.DocumentVersionId,
            prior.SourceLocator,
            prior.SourceText,
            request.PropositionText,
            MapAssertion(prior.AssertionTypeCode),
            prior.AttributedTo,
            prior.EffectiveAt);

        var context = new LpiIntegrationContext(
            request.TenantId,
            request.ReviewerUserId,
            prior.DecisionMatterId,
            request.DecisionContractRevision,
            request.CandidateSetRevision,
            request.HierarchyRevision,
            prior.DocumentVersionId,
            request.ReviewerUserId,
            request.ScoringConfigurationVersion,
            BuildReviseIdempotencyKey(prior, placements, request.PropositionText));

        return await integrationService.ApplyAsync(proposition, placements, context, LpiOperationKind.Revise, cancellationToken, prior.RetrievalModeCode);
    }

    public async Task<LpiIntegrationResult> WithdrawAsync(
        LpiReviewWithdrawRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prior = await repository.GetAcceptedPropositionAsync(
            request.TenantId, request.RetrievedPropositionId, cancellationToken);
        if (prior is null)
        {
            return new LpiIntegrationResult(
                false, null, [], false, "Rejected",
                "The proposition being withdrawn no longer exists in an accepted state for this tenant.");
        }

        var placements = prior.Placements.Select(p => new LpiPlacementProposal(
            prior.RetrievedPropositionId,
            HierarchyRevisionId: Guid.Empty,
            TargetNodeId: p.TargetNodeId,
            LeftNeighborId: p.LeftNeighborId,
            RightNeighborId: p.RightNeighborId,
            PlacementFraction: p.PlacementFraction,
            Relationship: MapRelationship(p.RelationshipCode),
            Rationale: p.Rationale)).ToList();

        var context = new LpiIntegrationContext(
            request.TenantId,
            request.ReviewerUserId,
            prior.DecisionMatterId,
            request.DecisionContractRevision,
            request.CandidateSetRevision,
            request.HierarchyRevision,
            prior.DocumentVersionId,
            request.ReviewerUserId,
            request.ScoringConfigurationVersion,
            BuildWithdrawIdempotencyKey(prior));

        // Idempotency: a replayed withdrawal returns the original op result.
        var existing = await repository.TryGetOperationAsync(request.TenantId, context.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return existing with { StatusCode = "Idempotent" };

        var commit = await repository.WithdrawAcceptedAsync(new LpiWithdrawCommit(
            request.TenantId,
            request.ReviewerUserId,
            prior.DecisionMatterId,
            prior.RetrievedPropositionId,
            placements,
            context,
            request.Reason), cancellationToken);

        var status = commit.ReassessmentEnqueued ? "EvaluationPending" : "Applied";
        return new LpiIntegrationResult(
            true,
            commit.CommittedPropositionId,
            commit.SupersededPropositionIds,
            commit.ReassessmentEnqueued,
            status,
            commit.ReassessmentEnqueued
                ? "Proposition withdrawn; POLOXI Core reassessment enqueued. The displayed ranking will update once recompetition completes."
                : "Proposition withdrawn. No owning candidate was affected, so the ranking is unchanged.");
    }

    public Task RejectAsync(LpiReviewRejectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return repository.RejectReviewItemAsync(
            request.TenantId, request.ReviewerUserId, request.RetrievedPropositionId,
            request.Reason, cancellationToken);
    }

    private static bool IsReviewable(string stateCode) => stateCode switch
    {
        "Extracted" or "PlacementProposed" or "ReviewRequired" or "NeedsHierarchyReview" => true,
        _ => false,
    };

    // Stable idempotency identity: accepting the SAME proposition at the SAME placements yields the same
    // key, so a replayed accept returns the original ApplyAsync result rather than double-inserting.
    private static string BuildIdempotencyKey(LpiReviewItem item, IReadOnlyList<LpiPlacementProposal> placements)
    {
        var nodes = string.Join(",", placements.Select(p => p.TargetNodeId.ToString()).OrderBy(x => x, StringComparer.Ordinal));
        var material = string.Join("|", item.RetrievedPropositionId.ToString(), nodes);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"RETRIEVAL-ACCEPT:{Convert.ToHexString(hash)}";
    }

    // Revising the SAME prior proposition with the SAME corrected text + placements yields the same key,
    // so a replayed revision returns the original result rather than superseding twice.
    private static string BuildReviseIdempotencyKey(
        LpiReviewItem prior, IReadOnlyList<LpiPlacementProposal> placements, string correctedText)
    {
        var nodes = string.Join(",", placements.Select(p => $"{p.TargetNodeId}:{p.Relationship}").OrderBy(x => x, StringComparer.Ordinal));
        var material = string.Join("|", prior.RetrievedPropositionId.ToString(), correctedText, nodes);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"RETRIEVAL-REVISE:{Convert.ToHexString(hash)}";
    }

    // Withdrawing the SAME accepted proposition yields the same key, so a replayed withdrawal returns the
    // original result rather than retracting its contribution twice.
    private static string BuildWithdrawIdempotencyKey(LpiReviewItem prior)
    {
        var material = prior.RetrievedPropositionId.ToString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"RETRIEVAL-WITHDRAW:{Convert.ToHexString(hash)}";
    }

    private static LpiReviewItemView ToView(LpiReviewItem item) => new(
        item.RetrievedPropositionId,
        item.DecisionMatterId,
        item.DocumentVersionId,
        item.SourceLocator,
        item.SourceText,
        item.PropositionText,
        item.AssertionTypeCode,
        item.AttributedTo,
        item.EffectiveAt,
        item.StateCode,
        item.StateReason,
        item.Placements.Select(p => new LpiReviewPlacementView(
            p.TargetNodeId, p.LeftNeighborId, p.RightNeighborId,
            p.PlacementFraction, p.RelationshipCode, p.Rationale)).ToList(),
        item.RetrievalModeCode);

    private static LpiAssertionType MapAssertion(string code) => code switch
    {
        "Asserts" => LpiAssertionType.Asserts,
        "Reports" => LpiAssertionType.Reports,
        "Documents" => LpiAssertionType.Documents,
        "StatesLaw" => LpiAssertionType.StatesLaw,
        "Infers" => LpiAssertionType.Infers,
        _ => LpiAssertionType.Reports,
    };

    private static LpiRelationship MapRelationship(string code) => code switch
    {
        "SUPPORTS" => LpiRelationship.Supports,
        "CONTRADICTS" => LpiRelationship.Contradicts,
        "QUALIFIES" => LpiRelationship.Qualifies,
        "CONTEXT_ONLY" => LpiRelationship.ContextOnly,
        _ => LpiRelationship.ContextOnly,
    };
}
