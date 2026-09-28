using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Persistence for Continuous Decision Integrity (Phase 1). Backs the POLOXI.Legal_Decision* integrity
// tables from migration 0340. Kept separate from the large ILegalDecisionRepository to isolate the
// change-awareness concern. DB is the source of truth; snapshots are append-only (never overwritten).
// ─────────────────────────────────────────────────────────────────────────────────────────────
public interface IDecisionIntegrityRepository
{
    // ── Snapshots ──────────────────────────────────────────────────────────────────────────────
    Task<Guid> CreateSnapshotAsync(DecisionSnapshotPersistence snapshot, CancellationToken cancellationToken = default);
    Task<DecisionSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionSnapshotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionSnapshotDto>> GetMatterSnapshotsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<DecisionSnapshotDto?> GetLatestMatterSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<int> GetNextSnapshotNumberAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    // Updates ONLY the dynamic current-reliance status (readiness is immutable). Returns rows affected.
    Task<bool> UpdateSnapshotRelianceAsync(Guid tenantId, Guid userId, Guid decisionSnapshotId, string relianceStatusCode, string? relianceReason, CancellationToken cancellationToken = default);

    // ── Proposition ↔ evidence links + dependencies ─────────────────────────────────────────────
    Task SavePropositionEvidenceLinksAsync(IReadOnlyCollection<PropositionEvidenceLinkPersistence> links, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PropositionEvidenceLinkPersistence>> GetPropositionEvidenceLinksAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task SaveDependenciesAsync(IReadOnlyCollection<MatterDependencyPersistence> dependencies, CancellationToken cancellationToken = default);
    // Traverses the bounded matter-level dependency graph: which dependents rely on a given proposition/evidence.
    Task<IReadOnlyCollection<string>> GetDependentKeysForPropositionAsync(Guid tenantId, Guid decisionMatterId, string dependsOnKindCode, string dependsOnKey, CancellationToken cancellationToken = default);

    // ── Change events (idempotent) ──────────────────────────────────────────────────────────────
    // Returns the existing event id if the idempotency key already exists (no duplicate processing).
    Task<(Guid EventId, bool AlreadyExisted)> CreateChangeEventAsync(MatterChangeEventPersistence changeEvent, CancellationToken cancellationToken = default);
    Task UpdateChangeEventOutcomeAsync(Guid tenantId, Guid userId, Guid matterChangeEventId, string classificationCode, string processingStatusCode, string? processingError, string? summary, string? candidateFactsJson, int affectedPropositionCount, int affectedCandidateCount, CancellationToken cancellationToken = default);
    Task<MatterChangeEventDto?> GetChangeEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MatterChangeEventDto>> GetMatterChangeEventsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // ── Impacts ────────────────────────────────────────────────────────────────────────────────
    Task SaveImpactsAsync(IReadOnlyCollection<DecisionImpactPersistence> impacts, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionImpactDto>> GetImpactsForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default);

    // ── Review tasks ───────────────────────────────────────────────────────────────────────────
    Task<Guid> CreateReviewTaskAsync(DecisionReviewTaskPersistence reviewTask, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionReviewTaskDto>> GetReviewTasksForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionReviewTaskDto>> GetOpenReviewTasksAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<bool> UpdateReviewTaskStatusAsync(Guid tenantId, Guid userId, Guid decisionReviewTaskId, string statusCode, string? resolutionNotes, CancellationToken cancellationToken = default);

    // ── Workspace summaries ────────────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<MatterChangeReviewSummaryDto>> GetChangeReviewSummariesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // ── Decision Change Intelligence — first-class DecisionDelta (migration 0344; append-only) ────────
    // One immutable "what changed" record per material reevaluation. Never scores; it is a durable
    // projection of an already-computed recompetition used by the "what changed since" attorney timeline.
    Task<Guid> CreateDecisionDeltaAsync(DecisionDeltaPersistence delta, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionDeltaDto>> GetMatterDecisionDeltasAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<DecisionDeltaDto?> GetDecisionDeltaForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default);
}
