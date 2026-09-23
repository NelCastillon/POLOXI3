using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-4 closed-loop orchestrator tests (§18, §27, §33).
//
// Pins the loop invariants:
//   • A NEW verification event with a bound edge runs propagation and yields a research need.
//   • An idempotent replay records no event and skips propagation entirely.
//   • A claim with no edge binding verifies + persists but never drives propagation.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicClosedLoopOrchestratorTests
{
    private static readonly EpistemicAuthoritySettings Settings = new();

    private static ClaimProposition Claim(Guid? sourceBranchId) => new()
    {
        ClaimId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Text = "claim",
        NormalizedText = "claim",
        ClaimType = ClaimType.Factual,
        Origin = ClaimOrigin.LlmGenerated,
        VerificationState = ClaimVerificationState.Proposed,
        Materiality = 0.9m,
        DecisionImpact = 0.6m,
        SourceBranchId = sourceBranchId,
        Version = 1,
    };

    private static ClaimSupportRef VerifiedSupport(Guid claimId) => new()
    {
        SupportId = Guid.NewGuid(),
        ClaimId = claimId,
        EvidenceId = Guid.NewGuid(),
        Relationship = ClaimSupportRelationship.Supports,
        Strength = 0.9m,
        IndependentlyVerified = true,
    };

    private static DecisionGraphPersistence Graph(Guid edgeId, Guid branchId)
    {
        var sourceNode = Guid.NewGuid();
        var targetNode = Guid.NewGuid();
        var nodes = new[]
        {
            new DecisionGraphNodePersistence(sourceNode, "EVIDENCE", "E1", "Evidence", "stmt", 0.5m, false, false, "UNVERIFIED", 0),
            new DecisionGraphNodePersistence(targetNode, "PROPOSITION", "P1", "Proposition", "stmt", 0.5m, true, false, "UNVERIFIED", 1),
        };
        var edge = new DecisionGraphEdgePersistence(
            edgeId, "SUPPORTS", "EVIDENCE", sourceNode, "PROPOSITION", targetNode,
            SupportWeight: 0.8m, Materiality: 0.7m, IsEssential: true, IsDispositive: false,
            VerificationStatus: "UNVERIFIED", VerificationNotes: null, PropagatedStateCode: null)
        {
            SourceBranchId = branchId,
        };
        return new DecisionGraphPersistence(
            Guid.NewGuid(), Guid.NewGuid(), null, false, null, nodes, [edge], null);
    }

    private static DecisionBranchPersistence Branch(Guid branchId) => new(
        branchId, null, 1, "B1", "Frontier issue", "interp", DecisionBranchStates.Active,
        InformationValue: 0.8m, DecisionRelevance: 0.7m, FlipPotential: 0.6m, EvidenceAvailability: 0.3m,
        AdvScore: 0.5m, Cost: 0.1m, IsOnFrontier: true, StopReason: null, SortOrder: 0);

    private static EpistemicClosedLoopOrchestrator NewOrchestrator(FakeRepo repo) =>
        new(new MaterialClaimVerificationService(repo, new ClaimAuthorityGate(), Settings),
            new DependencyPropagationService(),
            Settings);

    [Fact]
    public async Task BoundVerifiedClaim_RunsPropagation_AndProducesResearchNeed()
    {
        var repo = new FakeRepo();
        var orchestrator = NewOrchestrator(repo);
        var branchId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        var claim = Claim(branchId);

        var result = await orchestrator.RunAsync(new ClaimLoopRequest
        {
            Verification = new ClaimVerificationRequest
            {
                Claim = claim,
                TenantId = Guid.NewGuid(),
                Support = [VerifiedSupport(claim.ClaimId)],
            },
            Graph = Graph(edgeId, branchId),
            Branches = [Branch(branchId)],
            SessionId = claim.SessionId,
        });

        Assert.True(result.VerificationEventRecorded);
        Assert.True(result.PropagationRan);
        Assert.Equal(edgeId, result.BoundEdgeId);
        Assert.NotNull(result.UpdatedModel);
        Assert.NotNull(result.ResearchNeed);
    }

    [Fact]
    public async Task IdempotentReplay_RecordsNoEvent_AndSkipsPropagation()
    {
        var repo = new FakeRepo();
        var orchestrator = NewOrchestrator(repo);
        var branchId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        var claim = Claim(branchId);
        var tenantId = Guid.NewGuid();
        var support = new[] { VerifiedSupport(claim.ClaimId) };

        ClaimLoopRequest Build() => new()
        {
            Verification = new ClaimVerificationRequest
            {
                Claim = claim, TenantId = tenantId, Support = support,
            },
            Graph = Graph(edgeId, branchId),
            Branches = [Branch(branchId)],
            SessionId = claim.SessionId,
        };

        await orchestrator.RunAsync(Build());
        var second = await orchestrator.RunAsync(Build());

        Assert.False(second.VerificationEventRecorded);
        Assert.False(second.PropagationRan);
        Assert.Single(repo.Events);
    }

    [Fact]
    public async Task ClaimWithoutEdgeBinding_Verifies_ButDoesNotPropagate()
    {
        var repo = new FakeRepo();
        var orchestrator = NewOrchestrator(repo);
        var edgeId = Guid.NewGuid();
        var claim = Claim(sourceBranchId: null); // no lineage → no binding

        var result = await orchestrator.RunAsync(new ClaimLoopRequest
        {
            Verification = new ClaimVerificationRequest
            {
                Claim = claim,
                TenantId = Guid.NewGuid(),
                Support = [VerifiedSupport(claim.ClaimId)],
            },
            Graph = Graph(edgeId, Guid.NewGuid()),
            Branches = [],
            SessionId = claim.SessionId,
        });

        Assert.True(result.VerificationEventRecorded);
        Assert.False(result.PropagationRan);
        Assert.Null(result.BoundEdgeId);
        Assert.Null(result.ResearchNeed);
    }

    private sealed class FakeRepo : IEpistemicClaimRepository
    {
        private readonly Dictionary<Guid, ClaimPropositionPersistence> _claims = [];
        private readonly Dictionary<Guid, List<ClaimSupportPersistence>> _support = [];
        private readonly HashSet<string> _keys = [];

        public List<ClaimVerificationEventPersistence> Events { get; } = [];

        public Task UpsertClaimAsync(ClaimPropositionPersistence claim, CancellationToken cancellationToken = default)
        {
            _claims[claim.ClaimId] = claim;
            return Task.CompletedTask;
        }

        public Task ReplaceSupportAsync(
            Guid claimId, IReadOnlyList<ClaimSupportPersistence> support, Guid tenantId, Guid? actorUserId,
            CancellationToken cancellationToken = default)
        {
            _support[claimId] = [.. support];
            return Task.CompletedTask;
        }

        public Task<bool> TryRecordVerificationEventAsync(
            ClaimVerificationEventPersistence @event, CancellationToken cancellationToken = default)
        {
            if (!_keys.Add(@event.IdempotencyKey))
                return Task.FromResult(false);
            Events.Add(@event);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<ClaimPropositionPersistence>> GetClaimsForSessionAsync(
            Guid sessionId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClaimPropositionPersistence>>(
                _claims.Values.Where(c => c.DecisionSessionId == sessionId).ToArray());

        public Task<ClaimPropositionPersistence?> GetClaimAsync(
            Guid claimId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_claims.TryGetValue(claimId, out var c) ? c : null);

        public Task<IReadOnlyList<ClaimSupportPersistence>> GetSupportForClaimAsync(
            Guid claimId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClaimSupportPersistence>>(
                _support.TryGetValue(claimId, out var s) ? s : []);
    }
}
