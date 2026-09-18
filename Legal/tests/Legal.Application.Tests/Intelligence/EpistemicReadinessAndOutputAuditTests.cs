using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-5 readiness + output-audit tests (§34, §35).
//
// Pins the invariants:
//   • An essential, material, unresolved claim blocks decision readiness.
//   • A resolved (authorized) session has no readiness blockers.
//   • Output referencing an unauthorized claim is a violation; a foreign/unknown claim is a violation.
//   • Both flags, when off, bypass enforcement (ready / clean with a note).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicReadinessAndOutputAuditTests
{
    private static readonly EpistemicAuthoritySettings Settings = new();
    private static readonly Guid Tenant = Guid.NewGuid();

    private static ClaimPropositionPersistence Row(
        Guid sessionId,
        ClaimVerificationState state,
        ClaimDecisionAuthority authority,
        bool essential,
        decimal materiality) => new(
        ClaimId: Guid.NewGuid(),
        DecisionSessionId: sessionId,
        MatterId: null,
        Text: "claim",
        NormalizedText: "claim",
        ClaimTypeCode: ClaimCodes.ToCode(ClaimType.Factual),
        ClaimOriginCode: ClaimCodes.ToCode(ClaimOrigin.LlmGenerated),
        VerificationStateCode: ClaimCodes.ToCode(state),
        DecisionAuthorityCode: ClaimCodes.ToCode(authority),
        VerificationStrength: 0m,
        Materiality: materiality,
        DecisionImpact: 0.6m,
        Discrimination: 0m,
        Uncertainty: 0m,
        IsEssential: essential,
        SourceBranchId: null,
        SourceCandidateId: null,
        ProposedByModel: null,
        PromptRunId: null,
        VerificationReason: null,
        Version: 1,
        TenantId: Tenant,
        ActorUserId: null);

    [Fact]
    public async Task EssentialUnresolvedClaim_BlocksReadiness()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        var claim = Row(sessionId, ClaimVerificationState.Unverified, ClaimDecisionAuthority.None,
            essential: true, materiality: 0.9m);
        repo.Add(claim);

        var evaluator = new DecisionReadinessEvaluator(repo, new ClaimAuthorityGate(), Settings);
        var result = await evaluator.EvaluateAsync(sessionId, Tenant);

        Assert.True(result.Enforced);
        Assert.False(result.IsReady);
        Assert.Single(result.Blockers);
        Assert.Equal(claim.ClaimId, result.Blockers[0].ClaimId);
    }

    [Fact]
    public async Task SupportedAuthorizedSession_IsReady()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        repo.Add(Row(sessionId, ClaimVerificationState.Supported, ClaimDecisionAuthority.Full,
            essential: true, materiality: 0.9m));

        var evaluator = new DecisionReadinessEvaluator(repo, new ClaimAuthorityGate(), Settings);
        var result = await evaluator.EvaluateAsync(sessionId, Tenant);

        Assert.True(result.IsReady);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public async Task ReadinessBlocking_Disabled_ReportsReady()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        repo.Add(Row(sessionId, ClaimVerificationState.Unverified, ClaimDecisionAuthority.None,
            essential: true, materiality: 0.9m));

        var settings = new EpistemicAuthoritySettings { UseClaimReadinessBlocking = false };
        var evaluator = new DecisionReadinessEvaluator(repo, new ClaimAuthorityGate(), settings);
        var result = await evaluator.EvaluateAsync(sessionId, Tenant);

        Assert.True(result.IsReady);
        Assert.False(result.Enforced);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public async Task UnauthorizedClaimInOutput_IsViolation()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        var unauthorized = Row(sessionId, ClaimVerificationState.Unverified, ClaimDecisionAuthority.None,
            essential: false, materiality: 0.2m);
        var authorized = Row(sessionId, ClaimVerificationState.Supported, ClaimDecisionAuthority.Full,
            essential: false, materiality: 0.8m);
        repo.Add(unauthorized);
        repo.Add(authorized);

        var auditor = new OutputClaimAuditor(repo, Settings);
        var result = await auditor.AuditAsync(sessionId, Tenant,
            [unauthorized.ClaimId, authorized.ClaimId]);

        Assert.True(result.Enforced);
        Assert.False(result.IsClean);
        Assert.Single(result.Violations);
        Assert.Equal(unauthorized.ClaimId, result.Violations[0].ClaimId);
    }

    [Fact]
    public async Task ForeignOrUnknownClaimInOutput_IsUnknownViolation()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        var otherSessionClaim = Row(Guid.NewGuid(), ClaimVerificationState.Supported,
            ClaimDecisionAuthority.Full, essential: false, materiality: 0.8m);
        repo.Add(otherSessionClaim);
        var strayId = Guid.NewGuid();

        var auditor = new OutputClaimAuditor(repo, Settings);
        var result = await auditor.AuditAsync(sessionId, Tenant,
            [otherSessionClaim.ClaimId, strayId]);

        Assert.False(result.IsClean);
        Assert.Equal(2, result.UnknownClaimIds.Count);
        Assert.Contains(otherSessionClaim.ClaimId, result.UnknownClaimIds);
        Assert.Contains(strayId, result.UnknownClaimIds);
    }

    [Fact]
    public async Task AuthorizedOutputOnly_IsClean()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        var authorized = Row(sessionId, ClaimVerificationState.Supported, ClaimDecisionAuthority.Full,
            essential: false, materiality: 0.8m);
        repo.Add(authorized);

        var auditor = new OutputClaimAuditor(repo, Settings);
        var result = await auditor.AuditAsync(sessionId, Tenant, [authorized.ClaimId]);

        Assert.True(result.IsClean);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task OutputAudit_Disabled_ReportsClean()
    {
        var sessionId = Guid.NewGuid();
        var repo = new FakeRepo();
        var unauthorized = Row(sessionId, ClaimVerificationState.Unverified, ClaimDecisionAuthority.None,
            essential: false, materiality: 0.2m);
        repo.Add(unauthorized);

        var settings = new EpistemicAuthoritySettings { UseOutputClaimAudit = false };
        var auditor = new OutputClaimAuditor(repo, settings);
        var result = await auditor.AuditAsync(sessionId, Tenant, [unauthorized.ClaimId]);

        Assert.True(result.IsClean);
        Assert.False(result.Enforced);
    }

    private sealed class FakeRepo : IEpistemicClaimRepository
    {
        private readonly Dictionary<Guid, ClaimPropositionPersistence> _claims = [];
        private readonly Dictionary<Guid, List<ClaimSupportPersistence>> _support = [];

        public void Add(ClaimPropositionPersistence claim) => _claims[claim.ClaimId] = claim;

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
