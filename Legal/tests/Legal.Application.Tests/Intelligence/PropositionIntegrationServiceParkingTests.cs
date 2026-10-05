using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Decision.Lpi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Gap 2 — a proposition that FAILS the validation gate (or a stale-hierarchy guard) must be PARKED for
// attorney triage, never silently discarded. These tests pin the orchestration promise that the
// service calls ParkForReviewAsync (not a bare reject-without-persist) and that a parked result:
//   • is NOT Applied and enqueues NO reassessment (an unreviewed proposition cannot change ranking),
//   • carries the preserved review state (ReviewRequired | NeedsHierarchyReview) as its status,
//   • surfaces the parked proposition id so the UI can route the attorney to correct and resubmit.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class PropositionIntegrationServiceParkingTests
{
    private static RetrievedProposition Proposition(string text = "The policy excludes flood damage.") =>
        new(
            ProposalId: Guid.NewGuid(),
            MatterId: Guid.NewGuid(),
            DocumentVersionId: Guid.NewGuid(),
            SourceLocator: "p.4 ¶2",
            SourceText: text,
            PropositionText: text,
            AssertionType: LpiAssertionType.Asserts,
            AttributedTo: null,
            EffectiveAt: null);

    private static LpiPlacementProposal Placement(
        LpiRelationship relationship = LpiRelationship.Supports,
        decimal? fraction = null,
        Guid? left = null,
        Guid? right = null) =>
        new(
            ProposalId: Guid.NewGuid(),
            HierarchyRevisionId: Guid.NewGuid(),
            TargetNodeId: Guid.NewGuid(),
            LeftNeighborId: left,
            RightNeighborId: right,
            PlacementFraction: fraction,
            Relationship: relationship,
            Rationale: "reviewed placement");

    private static LpiIntegrationContext Context(long hierarchyRevision = 0) =>
        new(
            TenantId: Guid.NewGuid(),
            ActorUserId: Guid.NewGuid(),
            DecisionMatterId: Guid.NewGuid(),
            DecisionContractRevision: 1,
            CandidateSetRevision: 1,
            HierarchyRevision: hierarchyRevision,
            SourceDocumentVersionId: Guid.NewGuid(),
            ReviewerUserId: Guid.NewGuid(),
            ScoringConfigurationVersion: "SCORE_V1",
            IdempotencyKey: Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NoPlacement_IsParked_AsNeedsHierarchyReview_WithoutReassessment()
    {
        var repo = new FakeRepository();
        var service = new PropositionIntegrationService(repo, new DisabledInitializer(), NullLogger<PropositionIntegrationService>.Instance);

        var result = await service.ApplyAsync(Proposition(), [], Context());

        Assert.False(result.Applied);
        Assert.False(result.ReassessmentEnqueued);
        Assert.Equal(nameof(LpiProposalState.NeedsHierarchyReview), result.StatusCode);
        Assert.NotNull(result.CommittedPropositionId);
        Assert.Equal(result.CommittedPropositionId, repo.LastParkedId);
        Assert.Equal(1, repo.ParkCount);
        Assert.Equal(0, repo.CommitCount);
        Assert.Equal(nameof(LpiProposalState.NeedsHierarchyReview), repo.LastPark!.ReviewStateCode);
    }

    [Fact]
    public async Task StaleHierarchy_IsParked_AsReviewRequired_WithoutReassessment()
    {
        var repo = new FakeRepository { CurrentHierarchyVersion = 9 };
        var service = new PropositionIntegrationService(repo, new DisabledInitializer(), NullLogger<PropositionIntegrationService>.Instance);

        // Context revision 5 ≠ current 9 ⇒ stale.
        var result = await service.ApplyAsync(Proposition(), [Placement()], Context(hierarchyRevision: 5));

        Assert.False(result.Applied);
        Assert.False(result.ReassessmentEnqueued);
        Assert.Equal(nameof(LpiProposalState.ReviewRequired), result.StatusCode);
        Assert.NotNull(result.CommittedPropositionId);
        Assert.Equal(1, repo.ParkCount);
        Assert.Equal(0, repo.CommitCount);
    }

    private sealed class DisabledInitializer : ILpiScoreInitializer
    {
        public bool AncestorInfluenceEnabled => false;
        public LpiScoreInitializerResult Compute(LpiScoreInitializerInput input) =>
            throw new InvalidOperationException("Compute must not be called when ancestor influence is disabled.");
    }

    private sealed class FakeRepository : ILpiPropositionIntegrationRepository
    {
        public long CurrentHierarchyVersion { get; set; }
        public int ParkCount { get; private set; }
        public int CommitCount { get; private set; }
        public Guid? LastParkedId { get; private set; }
        public LpiReviewPark? LastPark { get; private set; }

        public Task<LpiIntegrationResult?> TryGetOperationAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult<LpiIntegrationResult?>(null);

        public Task<long> GetHierarchyVersionAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentHierarchyVersion);

        public Task<IReadOnlyCollection<string>> GetAcceptedPropositionTextsAsync(Guid tenantId, Guid decisionMatterId, IReadOnlyCollection<Guid> targetNodeIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<IReadOnlyList<LpiAncestorScore>> GetAncestorScoresAsync(Guid tenantId, Guid decisionMatterId, Guid parentNodeId, int maxDepth, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LpiAncestorScore>>([]);

        public Task<LpiIntegrationCommitResult> CommitIntegrationAsync(LpiIntegrationCommit commit, CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(new LpiIntegrationCommitResult(Guid.NewGuid(), [], Guid.NewGuid(), true, 1, 0));
        }

        public Task<Guid> ParkForReviewAsync(LpiReviewPark park, CancellationToken cancellationToken = default)
        {
            ParkCount++;
            LastPark = park;
            LastParkedId = Guid.NewGuid();
            return Task.FromResult(LastParkedId.Value);
        }

        public Task<IReadOnlyList<LpiReviewItem>> GetPendingReviewItemsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LpiReviewItem>>([]);

        public Task<LpiReviewItem?> GetReviewItemAsync(Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
            => Task.FromResult<LpiReviewItem?>(null);

        public Task RejectReviewItemAsync(Guid tenantId, Guid actorUserId, Guid retrievedPropositionId, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<LpiReviewItem?> GetAcceptedPropositionAsync(Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
            => Task.FromResult<LpiReviewItem?>(null);

        public Task<LpiIntegrationCommitResult> WithdrawAcceptedAsync(LpiWithdrawCommit commit, CancellationToken cancellationToken = default)
            => Task.FromResult(new LpiIntegrationCommitResult(commit.RetrievedPropositionId, [commit.RetrievedPropositionId], Guid.NewGuid(), true, 1, 0));
    }
}
