using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity Phase 2 orchestration acceptance: after Phase 1 records candidate
// impacts, the reevaluation service must run POLOXI Core recompetition and — when the outcome changes —
// append a NEW superseding snapshot while marking the prior snapshot SUPERSEDED. History is never
// overwritten and the new snapshot is created unapproved (attorney stays in control).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionReevaluationServiceTests
{
    private static readonly Guid TenantId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MatterId = new("33333333-3333-3333-3333-333333333333");

    private static DecisionCoreSettings Settings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    private static DecisionCandidatePersistence Candidate(string code, string name, decimal composite, decimal verification, bool winner)
        => new(
            Guid.NewGuid(), code, name, name,
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.55m, AuthoritySupport: 0.55m,
            Verification: verification, Uncertainty: 0.30m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.95m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(string code)
        => new(
            Guid.NewGuid(), null, 1, code, code, null, "ACTIVE",
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: true, StopReason: null, SortOrder: 0);

    [Fact]
    public async Task MaterialContradiction_FlipsWinner_AppendsSupersedingSnapshot()
    {
        var changeEventId = Guid.NewGuid();
        var priorSnapshotId = Guid.NewGuid();
        var c1 = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var c2 = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);

        var repo = new RecordingIntegrityRepository
        {
            ChangeEvent = ChangeEvent(changeEventId, MatterChangeClassification.MaterialContradiction),
            LatestSnapshot = Snapshot(priorSnapshotId, number: 1),
            Impacts = new[] { CandidateImpact(changeEventId, "C1", DecisionImpactSeverity.Material) }
        };

        var service = new DecisionReevaluationService(repo);
        var branches = new[] { Branch("C1.B1"), Branch("C2.B1") };
        var reopenAllowed = new HashSet<Guid>(branches.Select(b => b.DecisionBranchId));

        var result = await service.ReevaluateChangeAsync(
            TenantId, UserId, MatterId, changeEventId, new[] { c1, c2 }, branches, Settings(), reopenAllowed);

        Assert.True(result.ReevaluationPerformed);
        Assert.True(result.OutcomeChanged);
        Assert.Equal(c1.DecisionCandidateId, result.PreviousWinnerId);
        Assert.Equal(c2.DecisionCandidateId, result.CurrentWinnerId);

        // A new snapshot was appended, unapproved, with CURRENT reliance.
        var created = Assert.Single(repo.CreatedSnapshots);
        Assert.Equal(result.NewSnapshotId, created.DecisionSnapshotId);
        Assert.False(created.IsAttorneyApproved);
        Assert.Equal(DecisionRelianceStatus.Current, created.RelianceStatusCode);
        Assert.Equal(2, created.SnapshotNumber);

        // The prior snapshot was marked SUPERSEDED (history preserved, not deleted).
        var reliance = Assert.Single(repo.RelianceUpdates);
        Assert.Equal(priorSnapshotId, reliance.SnapshotId);
        Assert.Equal(DecisionRelianceStatus.Superseded, reliance.StatusCode);

        // A first-class Decision Change Intelligence delta was recorded: winner changed, review required,
        // before/after winner labels captured, and linked to both the superseded and new snapshots.
        var delta = Assert.Single(repo.CreatedDeltas);
        Assert.Equal(DecisionDeltaKind.WinnerChanged, delta.DeltaKindCode);
        Assert.True(delta.WinnerChanged);
        Assert.True(delta.AttorneyReviewRequired);
        Assert.Equal(c1.DecisionCandidateId, delta.PreviousWinnerId);
        Assert.Equal(c2.DecisionCandidateId, delta.CurrentWinnerId);
        Assert.Equal("Acme prevails", delta.PreviousWinnerLabel);
        Assert.Equal("Globex prevails", delta.CurrentWinnerLabel);
        Assert.Equal(priorSnapshotId, delta.FromSnapshotId);
        Assert.Equal(result.NewSnapshotId, delta.ToSnapshotId);
        Assert.False(string.IsNullOrWhiteSpace(delta.RequiredAction));
    }

    [Fact]
    public async Task NoMaterialImpact_DoesNotReevaluate_OrCreateSnapshot()
    {
        var changeEventId = Guid.NewGuid();
        var repo = new RecordingIntegrityRepository
        {
            ChangeEvent = ChangeEvent(changeEventId, MatterChangeClassification.NoMaterialImpact),
            LatestSnapshot = Snapshot(Guid.NewGuid(), number: 1)
        };

        var service = new DecisionReevaluationService(repo);
        var c1 = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var c2 = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);

        var result = await service.ReevaluateChangeAsync(
            TenantId, UserId, MatterId, changeEventId, new[] { c1, c2 }, [], Settings(), new HashSet<Guid>());

        Assert.False(result.ReevaluationPerformed);
        Assert.False(result.OutcomeChanged);
        Assert.Empty(repo.CreatedSnapshots);
        Assert.Empty(repo.RelianceUpdates);
    }

    private static MatterChangeEventDto ChangeEvent(Guid id, string classification)
        => new(id, MatterId, MatterChangeSource.DocumentUpload, Guid.NewGuid(), Guid.NewGuid(),
            "Deposition transcript", DateTime.UtcNow, classification, Summary: null,
            MatterChangeProcessingStatus.Processed, ProcessingError: null,
            AffectedPropositionCount: 1, AffectedCandidateCount: 1, ProcessedDateUtc: DateTime.UtcNow,
            CreatedDateUtc: DateTime.UtcNow);

    private static DecisionSnapshotDto Snapshot(Guid id, int number)
        => new(id, MatterId, DecisionSessionId: Guid.NewGuid(), number, "Acme prevails", "Acme prevails.",
            ReadinessStatusCode: "READY", RelianceStatusCode: DecisionRelianceStatus.Current,
            RelianceReason: null, IsAttorneyApproved: false, SupersededBySnapshotId: null,
            EvaluatedDateUtc: DateTime.UtcNow, CreatedDateUtc: DateTime.UtcNow);

    private static DecisionImpactDto CandidateImpact(Guid changeEventId, string candidateCode, string severity)
        => new(Guid.NewGuid(), changeEventId, DecisionImpactKind.Candidate, candidateCode, candidateCode,
            "Previously evaluated", "Reevaluation required", severity, "depends on affected proposition");

    // Minimal in-memory integrity repository recording only what Phase 2 orchestration touches.
    private sealed class RecordingIntegrityRepository : IDecisionIntegrityRepository
    {
        public MatterChangeEventDto? ChangeEvent { get; set; }
        public DecisionSnapshotDto? LatestSnapshot { get; set; }
        public IReadOnlyCollection<DecisionImpactDto> Impacts { get; set; } = [];
        public List<DecisionSnapshotPersistence> CreatedSnapshots { get; } = [];
        public List<(Guid SnapshotId, string StatusCode, string? Reason)> RelianceUpdates { get; } = [];
        public List<DecisionDeltaPersistence> CreatedDeltas { get; } = [];

        public Task<Guid> CreateSnapshotAsync(DecisionSnapshotPersistence snapshot, CancellationToken cancellationToken = default)
        {
            CreatedSnapshots.Add(snapshot);
            return Task.FromResult(snapshot.DecisionSnapshotId);
        }

        public Task<DecisionSnapshotDto?> GetLatestMatterSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult(LatestSnapshot);

        public Task<int> GetNextSnapshotNumberAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult((LatestSnapshot?.SnapshotNumber ?? 0) + 1);

        public Task<bool> UpdateSnapshotRelianceAsync(Guid tenantId, Guid userId, Guid decisionSnapshotId, string relianceStatusCode, string? relianceReason, CancellationToken cancellationToken = default)
        {
            RelianceUpdates.Add((decisionSnapshotId, relianceStatusCode, relianceReason));
            return Task.FromResult(true);
        }

        public Task<MatterChangeEventDto?> GetChangeEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default)
            => Task.FromResult(ChangeEvent);

        public Task<IReadOnlyCollection<DecisionImpactDto>> GetImpactsForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default)
            => Task.FromResult(Impacts);

        // ── Unused by Phase 2 orchestration ──────────────────────────────────────────────────────
        public Task<DecisionSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionSnapshotId, CancellationToken cancellationToken = default) => Task.FromResult<DecisionSnapshotDto?>(null);
        public Task<IReadOnlyCollection<DecisionSnapshotDto>> GetMatterSnapshotsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<DecisionSnapshotDto>>([]);
        public Task SavePropositionEvidenceLinksAsync(IReadOnlyCollection<PropositionEvidenceLinkPersistence> links, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<PropositionEvidenceLinkPersistence>> GetPropositionEvidenceLinksAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PropositionEvidenceLinkPersistence>>([]);
        public Task SaveDependenciesAsync(IReadOnlyCollection<MatterDependencyPersistence> dependencies, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<string>> GetDependentKeysForPropositionAsync(Guid tenantId, Guid decisionMatterId, string dependsOnKindCode, string dependsOnKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
        public Task<(Guid EventId, bool AlreadyExisted)> CreateChangeEventAsync(MatterChangeEventPersistence changeEvent, CancellationToken cancellationToken = default) => Task.FromResult((changeEvent.MatterChangeEventId, false));
        public Task UpdateChangeEventOutcomeAsync(Guid tenantId, Guid userId, Guid matterChangeEventId, string classificationCode, string processingStatusCode, string? processingError, string? summary, string? candidateFactsJson, int affectedPropositionCount, int affectedCandidateCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<MatterChangeEventDto>> GetMatterChangeEventsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<MatterChangeEventDto>>([]);
        public Task SaveImpactsAsync(IReadOnlyCollection<DecisionImpactPersistence> impacts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Guid> CreateReviewTaskAsync(DecisionReviewTaskPersistence reviewTask, CancellationToken cancellationToken = default) => Task.FromResult(reviewTask.DecisionReviewTaskId);
        public Task<IReadOnlyCollection<DecisionReviewTaskDto>> GetReviewTasksForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<DecisionReviewTaskDto>>([]);
        public Task<IReadOnlyCollection<DecisionReviewTaskDto>> GetOpenReviewTasksAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<DecisionReviewTaskDto>>([]);
        public Task<bool> UpdateReviewTaskStatusAsync(Guid tenantId, Guid userId, Guid decisionReviewTaskId, string statusCode, string? resolutionNotes, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyCollection<MatterChangeReviewSummaryDto>> GetChangeReviewSummariesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<MatterChangeReviewSummaryDto>>([]);

        public Task<Guid> CreateDecisionDeltaAsync(DecisionDeltaPersistence delta, CancellationToken cancellationToken = default)
        {
            CreatedDeltas.Add(delta);
            return Task.FromResult(delta.DecisionDeltaId);
        }
        public Task<IReadOnlyCollection<DecisionDeltaDto>> GetMatterDecisionDeltasAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<DecisionDeltaDto>>([]);
        public Task<DecisionDeltaDto?> GetDecisionDeltaForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default) => Task.FromResult<DecisionDeltaDto?>(null);
    }
}
