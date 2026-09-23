using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-3 material-claim verification bridge tests (§18, §27, §33).
//
// Pins the bridge invariants between claim verification and the V2.1 dependency-propagation loop:
//   • Only independently-verified support grants authority (RetrievedSource ≠ Verified).
//   • Absence of support classifies Unverified (never Contradicted) and maps to an UNVERIFIED edge.
//   • Verified support maps to VERIFIED; contradiction maps to INVALIDATED with a negative signal.
//   • The verification event is idempotent so propagation runs exactly once per distinct transition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class MaterialClaimVerificationServiceTests
{
    private static readonly EpistemicAuthoritySettings Settings = new();

    private static ClaimProposition Claim(bool essential = false) => new()
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
        IsEssential = essential,
        Version = 1,
    };

    private static ClaimSupportRef Support(
        Guid claimId,
        ClaimSupportRelationship relationship,
        decimal strength,
        bool verified) => new()
        {
            SupportId = Guid.NewGuid(),
            ClaimId = claimId,
            EvidenceId = Guid.NewGuid(),
            Relationship = relationship,
            Strength = strength,
            IndependentlyVerified = verified,
        };

    private static MaterialClaimVerificationService NewService(FakeRepo repo) =>
        new(repo, new ClaimAuthorityGate(), Settings);

    [Fact]
    public async Task VerifiedSupport_ResolvesSupported_AndMapsToVerifiedEdge()
    {
        var repo = new FakeRepo();
        var service = NewService(repo);
        var claim = Claim();

        var outcome = await service.VerifyAsync(new ClaimVerificationRequest
        {
            Claim = claim,
            TenantId = Guid.NewGuid(),
            Support = [Support(claim.ClaimId, ClaimSupportRelationship.Supports, 0.9m, verified: true)],
        });

        Assert.Equal(ClaimVerificationState.Supported, outcome.NewState);
        Assert.Equal(DecisionVerificationStates.Verified, outcome.MappedEdgeStatus);
        Assert.True(outcome.StateChanged);
        Assert.True(outcome.VerificationEventRecorded);
    }

    [Fact]
    public async Task AssertedButUnverifiedSupport_DoesNotGrantAuthority_MapsUnverified()
    {
        // RetrievedSource ≠ VerifiedEvidence: strong strength but not independently verified.
        var repo = new FakeRepo();
        var service = NewService(repo);
        var claim = Claim();

        var outcome = await service.VerifyAsync(new ClaimVerificationRequest
        {
            Claim = claim,
            TenantId = Guid.NewGuid(),
            Support = [Support(claim.ClaimId, ClaimSupportRelationship.Supports, 1m, verified: false)],
        });

        Assert.Equal(ClaimVerificationState.Unverified, outcome.NewState);
        Assert.Equal(DecisionVerificationStates.Unverified, outcome.MappedEdgeStatus);
        Assert.Equal(ClaimDecisionAuthority.None, outcome.Authority.DecisionAuthority);
        Assert.Equal(0m, outcome.Claim.VerificationStrength);
    }

    [Fact]
    public async Task NoSupport_ClassifiesUnverified_NeverContradicted()
    {
        var repo = new FakeRepo();
        var service = NewService(repo);
        var claim = Claim();

        var outcome = await service.VerifyAsync(new ClaimVerificationRequest
        {
            Claim = claim,
            TenantId = Guid.NewGuid(),
            Support = [],
        });

        Assert.Equal(ClaimVerificationState.Unverified, outcome.NewState);
        Assert.NotEqual(ClaimVerificationState.Contradicted, outcome.NewState);
    }

    [Fact]
    public async Task VerifiedContradiction_MapsToInvalidated_WithNegativeSignal()
    {
        var repo = new FakeRepo();
        var service = NewService(repo);
        var claim = Claim();

        var outcome = await service.VerifyAsync(new ClaimVerificationRequest
        {
            Claim = claim,
            TenantId = Guid.NewGuid(),
            Support = [Support(claim.ClaimId, ClaimSupportRelationship.Contradicts, 0.9m, verified: true)],
        });

        Assert.Equal(ClaimVerificationState.Contradicted, outcome.NewState);
        Assert.Equal(DecisionVerificationStates.Invalidated, outcome.MappedEdgeStatus);
        Assert.True(outcome.Authority.AllowedNegativeContribution > 0m);
        Assert.Equal(0m, outcome.Authority.AllowedPositiveContribution);
    }

    [Fact]
    public async Task ReplayedVerification_IsIdempotent_EventRecordedOnce()
    {
        var repo = new FakeRepo();
        var service = NewService(repo);
        var claim = Claim();
        var tenantId = Guid.NewGuid();
        var support = new[] { Support(claim.ClaimId, ClaimSupportRelationship.Supports, 0.9m, verified: true) };

        var first = await service.VerifyAsync(new ClaimVerificationRequest
        {
            Claim = claim, TenantId = tenantId, Support = support,
        });
        var second = await service.VerifyAsync(new ClaimVerificationRequest
        {
            Claim = claim, TenantId = tenantId, Support = support,
        });

        Assert.True(first.VerificationEventRecorded);
        Assert.False(second.VerificationEventRecorded);
        Assert.Single(repo.Events);
    }

    // In-memory repository capturing writes; models the idempotency-key uniqueness of the event table.
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
            Guid claimId,
            IReadOnlyList<ClaimSupportPersistence> support,
            Guid tenantId,
            Guid? actorUserId,
            CancellationToken cancellationToken = default)
        {
            _support[claimId] = [.. support];
            return Task.CompletedTask;
        }

        public Task<bool> TryRecordVerificationEventAsync(
            ClaimVerificationEventPersistence @event,
            CancellationToken cancellationToken = default)
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
