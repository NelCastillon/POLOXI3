using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-6 decision-bridge tests (§34, §35).
//
// Pins the advisory-overlay invariants:
//   • Assertion nodes (PROPOSITION/FACT) are projected into authoritative claims; other node kinds are not.
//   • A VERIFIED support edge grants POLOXI authority; an unverified-only claim gains none.
//   • Readiness + output audit run over the projected claims.
//   • The master flag off, and any downstream failure, leave the pipeline unaffected (non-blocking).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicDecisionBridgeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static EpistemicDecisionBridge Build(IEpistemicClaimRepository repo, EpistemicAuthoritySettings settings)
    {
        var gate = new ClaimAuthorityGate();
        var verification = new MaterialClaimVerificationService(repo, gate, settings);
        var readiness = new DecisionReadinessEvaluator(repo, gate, settings);
        var audit = new OutputClaimAuditor(repo, settings);
        return new EpistemicDecisionBridge(
            verification, readiness, audit, settings, NullLogger<EpistemicDecisionBridge>.Instance);
    }

    private static DecisionGraphNodeDto Node(Guid id, string kind, bool essential = false, decimal support = 0.8m)
        => new(id, kind, kind, $"{kind} node", $"{kind} statement", support, essential, false,
            DecisionVerificationStates.Unverified, 0);

    private static DecisionGraphEdgeDto Edge(Guid id, Guid source, Guid target, string verification)
        => new(id, DecisionGraphRelations.Supports, DecisionGraphNodeKinds.Evidence, source,
            DecisionGraphNodeKinds.Proposition, target, 0.9m, 0.9m, true, false, verification, null, null);

    [Fact]
    public async Task Bridge_ProjectsAssertionNodesOnly()
    {
        var sessionId = Guid.NewGuid();
        var proposition = Guid.NewGuid();
        var evidence = Guid.NewGuid();
        var repo = new FakeRepo();
        var bridge = Build(repo, new EpistemicAuthoritySettings());

        var context = new EpistemicDecisionContext
        {
            SessionId = sessionId,
            TenantId = Tenant,
            Nodes =
            [
                Node(proposition, DecisionGraphNodeKinds.Proposition),
                Node(evidence, DecisionGraphNodeKinds.Evidence),
            ],
            Edges = [],
        };

        var result = await bridge.ProjectAndGovernAsync(context);

        Assert.True(result.Executed);
        Assert.Equal(1, result.ProjectedClaimCount);
        Assert.NotNull(repo.Get(proposition));
        Assert.Null(repo.Get(evidence));
    }

    [Fact]
    public async Task Bridge_VerifiedSupport_GrantsAuthority()
    {
        var sessionId = Guid.NewGuid();
        var proposition = Guid.NewGuid();
        var evidence = Guid.NewGuid();
        var repo = new FakeRepo();
        var bridge = Build(repo, new EpistemicAuthoritySettings());

        var context = new EpistemicDecisionContext
        {
            SessionId = sessionId,
            TenantId = Tenant,
            Nodes = [Node(proposition, DecisionGraphNodeKinds.Proposition, essential: true)],
            Edges = [Edge(Guid.NewGuid(), evidence, proposition, DecisionVerificationStates.Verified)],
        };

        var result = await bridge.ProjectAndGovernAsync(context);

        Assert.Equal(1, result.AuthorizedClaimCount);
        Assert.NotNull(result.Readiness);
        Assert.True(result.Readiness!.IsReady);
        Assert.NotNull(result.OutputAudit);
        Assert.True(result.OutputAudit!.IsClean);
    }

    [Fact]
    public async Task Bridge_UnverifiedClaim_GainsNoAuthority()
    {
        var sessionId = Guid.NewGuid();
        var proposition = Guid.NewGuid();
        var repo = new FakeRepo();
        var bridge = Build(repo, new EpistemicAuthoritySettings());

        var context = new EpistemicDecisionContext
        {
            SessionId = sessionId,
            TenantId = Tenant,
            Nodes = [Node(proposition, DecisionGraphNodeKinds.Proposition)],
            Edges = [],
        };

        var result = await bridge.ProjectAndGovernAsync(context);

        Assert.Equal(1, result.ProjectedClaimCount);
        Assert.Equal(0, result.AuthorizedClaimCount);
        // The unauthorized claim surfaced in output is an audit violation.
        Assert.NotNull(result.OutputAudit);
        Assert.False(result.OutputAudit!.IsClean);
    }

    [Fact]
    public async Task Bridge_Disabled_SkipsWithoutProjecting()
    {
        var sessionId = Guid.NewGuid();
        var proposition = Guid.NewGuid();
        var repo = new FakeRepo();
        var bridge = Build(repo, new EpistemicAuthoritySettings { UseEpistemicDecisionBridge = false });

        var context = new EpistemicDecisionContext
        {
            SessionId = sessionId,
            TenantId = Tenant,
            Nodes = [Node(proposition, DecisionGraphNodeKinds.Proposition)],
            Edges = [],
        };

        var result = await bridge.ProjectAndGovernAsync(context);

        Assert.False(result.Executed);
        Assert.Equal(0, result.ProjectedClaimCount);
        Assert.Null(repo.Get(proposition));
    }

    // EA-7: In the default Advisory mode, EA may consider the decision NOT ready, but it must never
    // change the authoritative verdict — OverrideApplied stays false regardless of EA readiness.
    [Fact]
    public async Task Bridge_AdvisoryMode_NeverAppliesOverride()
    {
        var sessionId = Guid.NewGuid();
        var proposition = Guid.NewGuid();
        var repo = new FakeRepo();
        // Advisory is the default; an unauthorized claim makes EA not-ready but must not override.
        var bridge = Build(repo, new EpistemicAuthoritySettings());

        var context = new EpistemicDecisionContext
        {
            SessionId = sessionId,
            TenantId = Tenant,
            Nodes = [Node(proposition, DecisionGraphNodeKinds.Proposition, essential: true)],
            Edges = [],
        };

        var result = await bridge.ProjectAndGovernAsync(context);

        Assert.Equal(EpistemicOverrideMode.Advisory, result.OverrideMode);
        Assert.False(result.OverrideApplied);
    }

    // EA-7: Every involved claim (authorized AND unauthorized) is surfaced for reference — nothing is
    // hidden or dropped. Each carries a non-empty annotation for the UI.
    [Fact]
    public async Task Bridge_SurfacesAllInvolvedClaims_Annotated()
    {
        var sessionId = Guid.NewGuid();
        var authorized = Guid.NewGuid();
        var unauthorized = Guid.NewGuid();
        var evidence = Guid.NewGuid();
        var repo = new FakeRepo();
        var bridge = Build(repo, new EpistemicAuthoritySettings());

        var context = new EpistemicDecisionContext
        {
            SessionId = sessionId,
            TenantId = Tenant,
            Nodes =
            [
                Node(authorized, DecisionGraphNodeKinds.Proposition, essential: true),
                Node(unauthorized, DecisionGraphNodeKinds.Proposition),
            ],
            Edges = [Edge(Guid.NewGuid(), evidence, authorized, DecisionVerificationStates.Verified)],
        };

        var result = await bridge.ProjectAndGovernAsync(context);

        Assert.Equal(2, result.ProjectedClaimCount);
        Assert.Equal(2, result.InvolvedClaims.Count);
        Assert.Contains(result.InvolvedClaims, c => c.ClaimId == authorized && c.IsAuthorized);
        Assert.Contains(result.InvolvedClaims, c => c.ClaimId == unauthorized && !c.IsAuthorized);
        Assert.All(result.InvolvedClaims, c => Assert.False(string.IsNullOrWhiteSpace(c.Annotation)));
    }

    private sealed class FakeRepo : IEpistemicClaimRepository
    {
        private readonly Dictionary<Guid, ClaimPropositionPersistence> _claims = [];
        private readonly Dictionary<Guid, List<ClaimSupportPersistence>> _support = [];

        public ClaimPropositionPersistence? Get(Guid claimId) =>
            _claims.TryGetValue(claimId, out var c) ? c : null;

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
            ClaimVerificationEventPersistence @event, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

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
