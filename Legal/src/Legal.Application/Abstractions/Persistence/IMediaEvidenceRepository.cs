using Legal.Application.Features.Intelligence.Decision.Media;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Persistence for the Media & Machine Evidence channel (Phase 3, Slice 1).
//
// Backs POLOXI.Legal_MediaAsset / Legal_MediaAssetVersion / Legal_MediaDerivative /
// Legal_EvidenceAnchor / Legal_PropositionEvidenceLink / Legal_MediaProcessingRun (migration 0401).
// All reads/writes are tenant + matter scoped. This repository owns ONLY the media asset/anchor/run
// graph; accepted propositions still flow through ILpiPropositionIntegrationRepository and the shared
// IPropositionIntegrationService. No scoring happens here — POLOXI Core owns outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IMediaEvidenceRepository
{
    // Register an asset + its first immutable version (bytes already stored; StorageKey/hash supplied).
    // The caller supplies the asset/version ids it already used to build the storage key.
    Task<MediaAssetRegistrationResult> RegisterAssetAsync(
        MediaAssetRegistration registration,
        Guid mediaAssetId,
        Guid mediaAssetVersionId,
        string storageKey,
        string contentHash,
        long byteLength,
        CancellationToken cancellationToken = default);

    // List assets for a matter (tenant scoped, not soft-deleted).
    Task<IReadOnlyList<MediaAsset>> GetAssetsAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Load one asset (tenant scoped) or null.
    Task<MediaAsset?> GetAssetAsync(
        Guid tenantId, Guid mediaAssetId, CancellationToken cancellationToken = default);

    // Versions of an asset, newest first.
    Task<IReadOnlyList<MediaAssetVersion>> GetVersionsAsync(
        Guid tenantId, Guid mediaAssetId, CancellationToken cancellationToken = default);

    // Load one version (tenant scoped) or null — used for authorized preview/range delivery.
    Task<MediaAssetVersion?> GetVersionAsync(
        Guid tenantId, Guid mediaAssetVersionId, CancellationToken cancellationToken = default);

    // Derivatives of a version.
    Task<IReadOnlyList<MediaDerivative>> GetDerivativesAsync(
        Guid tenantId, Guid mediaAssetVersionId, CancellationToken cancellationToken = default);

    // Insert a derivative produced by a processing run (or user-provided).
    Task<Guid> AddDerivativeAsync(
        MediaDerivative derivative, Guid tenantId, Guid actorUserId, CancellationToken cancellationToken = default);

    // Create a processing run idempotently. If the (tenant, idempotencyKey) already exists, return the
    // existing run id instead of starting a duplicate (retry-safe dispatch).
    Task<MediaProcessingRun> CreateProcessingRunAsync(
        MediaProcessingRun run, Guid actorUserId, CancellationToken cancellationToken = default);

    // Load one processing run (tenant scoped) or null.
    Task<MediaProcessingRun?> GetProcessingRunAsync(
        Guid tenantId, Guid mediaProcessingRunId, CancellationToken cancellationToken = default);

    // Update a run's status/coverage/error/timestamps as it progresses.
    Task UpdateProcessingRunAsync(
        Guid tenantId, Guid actorUserId, Guid mediaProcessingRunId, MediaProcessingStatus status,
        string? coverageJson, string? errorCode, string? errorMessage,
        DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc,
        CancellationToken cancellationToken = default);

    // Insert a validated evidence anchor. Bounds/tenant/matter must already be validated by the caller.
    Task<Guid> AddAnchorAsync(
        EvidenceAnchor anchor, Guid actorUserId, CancellationToken cancellationToken = default);

    // Load one anchor (tenant scoped) or null — used to reopen the exact region/interval/records.
    Task<EvidenceAnchor?> GetAnchorAsync(
        Guid tenantId, Guid evidenceAnchorId, CancellationToken cancellationToken = default);

    // Insert a proposition↔anchor evidence link (review status pending by default).
    Task<Guid> AddEvidenceLinkAsync(
        PropositionEvidenceLink link, Guid actorUserId, CancellationToken cancellationToken = default);

    // Evidence links for a parked/accepted proposition (tenant scoped).
    Task<IReadOnlyList<PropositionEvidenceLink>> GetEvidenceLinksAsync(
        Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default);

    // Update one link's reviewer confirmation state.
    Task SetEvidenceLinkReviewAsync(
        Guid tenantId, Guid actorUserId, Guid propositionEvidenceLinkId, EvidenceLinkReviewStatus status,
        CancellationToken cancellationToken = default);
}
