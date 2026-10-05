using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Decision.Lpi;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Document-Retrieval acceptance half. These tests pin the single-funnel promise: an attorney ACCEPT of
// a parked retrieved proposition must flow through the SHARED IPropositionIntegrationService.ApplyAsync
// (never a second scoring engine), and a REJECT must preserve the proposition (state update only) and
// must NOT invoke the integration funnel.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class RetrievalPropositionReviewServiceTests
{
    private static LpiReviewItem ParkedItem(string relationship = "SUPPORTS") => new(
        RetrievedPropositionId: Guid.NewGuid(),
        DecisionMatterId: Guid.NewGuid(),
        DocumentVersionId: Guid.NewGuid(),
        SourceLocator: "p.4 ¶2",
        SourceText: "The policy excludes flood damage.",
        PropositionText: "The policy excludes flood damage.",
        AssertionTypeCode: "Asserts",
        AttributedTo: null,
        EffectiveAt: null,
        StateCode: "PlacementProposed",
        StateReason: null,
        Placements: [new LpiReviewPlacement(
            TargetNodeId: Guid.NewGuid(),
            LeftNeighborId: null,
            RightNeighborId: null,
            PlacementFraction: null,
            RelationshipCode: relationship,
            Rationale: "Directly addresses the exclusion condition.",
            HierarchyRevision: 7)]);

    [Fact]
    public async Task Accept_routes_through_shared_integration_funnel()
    {
        var item = ParkedItem();
        var repo = new FakeReviewRepository { Item = item };
        var funnel = new SpyIntegrationService();
        var sut = new RetrievalPropositionReviewService(repo, funnel);

        var result = await sut.AcceptAsync(new LpiReviewAcceptRequest(
            TenantId: Guid.NewGuid(),
            ReviewerUserId: Guid.NewGuid(),
            RetrievedPropositionId: item.RetrievedPropositionId,
            DecisionContractRevision: 1,
            CandidateSetRevision: 1,
            HierarchyRevision: 7,
            ScoringConfigurationVersion: "v1"));

        Assert.Equal(1, funnel.ApplyCount);
        Assert.Equal(0, repo.RejectCount);
        Assert.Single(funnel.LastPlacements!);
        Assert.Equal(LpiRelationship.Supports, funnel.LastPlacements![0].Relationship);
        Assert.True(result.Applied);
    }

    [Fact]
    public async Task Accept_nonreviewable_state_does_not_call_funnel()
    {
        var item = ParkedItem() with { StateCode = "Accepted" };
        var repo = new FakeReviewRepository { Item = item };
        var funnel = new SpyIntegrationService();
        var sut = new RetrievalPropositionReviewService(repo, funnel);

        var result = await sut.AcceptAsync(new LpiReviewAcceptRequest(
            Guid.NewGuid(), Guid.NewGuid(), item.RetrievedPropositionId, 1, 1, 7, "v1"));

        Assert.Equal(0, funnel.ApplyCount);
        Assert.False(result.Applied);
        Assert.Equal("Rejected", result.StatusCode);
    }

    [Fact]
    public async Task Reject_preserves_proposition_and_skips_funnel()
    {
        var item = ParkedItem();
        var repo = new FakeReviewRepository { Item = item };
        var funnel = new SpyIntegrationService();
        var sut = new RetrievalPropositionReviewService(repo, funnel);

        await sut.RejectAsync(new LpiReviewRejectRequest(
            Guid.NewGuid(), Guid.NewGuid(), item.RetrievedPropositionId, "OCR artifact, not a real exclusion."));

        Assert.Equal(1, repo.RejectCount);
        Assert.Equal(0, funnel.ApplyCount);
    }

    private sealed class SpyIntegrationService : IPropositionIntegrationService
    {
        public int ApplyCount { get; private set; }
        public IReadOnlyList<LpiPlacementProposal>? LastPlacements { get; private set; }

        public Task<LpiIntegrationResult> ApplyAsync(
            RetrievedProposition acceptedProposition,
            IReadOnlyList<LpiPlacementProposal> acceptedPlacements,
            LpiIntegrationContext integrationContext,
            LpiOperationKind operation = LpiOperationKind.Add,
            CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            LastPlacements = acceptedPlacements;
            return Task.FromResult(new LpiIntegrationResult(
                true, Guid.NewGuid(), [], true, "Applied", null));
        }
    }

    private sealed class FakeReviewRepository : ILpiPropositionIntegrationRepository
    {
        public LpiReviewItem? Item { get; set; }
        public int RejectCount { get; private set; }

        public Task<LpiReviewItem?> GetReviewItemAsync(Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Item);

        public Task<IReadOnlyList<LpiReviewItem>> GetPendingReviewItemsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LpiReviewItem>>(Item is null ? [] : [Item]);

        public Task RejectReviewItemAsync(Guid tenantId, Guid actorUserId, Guid retrievedPropositionId, string reason, CancellationToken cancellationToken = default)
        {
            RejectCount++;
            return Task.CompletedTask;
        }

        public Task<LpiIntegrationResult?> TryGetOperationAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult<LpiIntegrationResult?>(null);

        public Task<long> GetHierarchyVersionAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<IReadOnlyCollection<string>> GetAcceptedPropositionTextsAsync(Guid tenantId, Guid decisionMatterId, IReadOnlyCollection<Guid> targetNodeIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<IReadOnlyList<LpiAncestorScore>> GetAncestorScoresAsync(Guid tenantId, Guid decisionMatterId, Guid parentNodeId, int maxDepth, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LpiAncestorScore>>([]);

        public Task<LpiIntegrationCommitResult> CommitIntegrationAsync(LpiIntegrationCommit commit, CancellationToken cancellationToken = default)
            => Task.FromResult(new LpiIntegrationCommitResult(Guid.NewGuid(), [], Guid.NewGuid(), true, 1, 0));

        public Task<Guid> ParkForReviewAsync(LpiReviewPark park, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<LpiReviewItem?> GetAcceptedPropositionAsync(Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Item);

        public Task<LpiIntegrationCommitResult> WithdrawAcceptedAsync(LpiWithdrawCommit commit, CancellationToken cancellationToken = default)
            => Task.FromResult(new LpiIntegrationCommitResult(commit.RetrievedPropositionId, [commit.RetrievedPropositionId], Guid.NewGuid(), true, 1, 0));
    }
}
