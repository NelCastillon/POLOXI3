using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

// DB-backed persistence for the Decision Contract aggregate. Tenant-scoped on every query/mutation.
// POLOXI remains the authoritative evaluator; this repository never computes or overrides scoring.
public interface ILegalDecisionContractRepository
{
    // Reads the current authoritative contract for a matter (ACTIVE if present, else latest DRAFT/version).
    Task<DecisionContractDto?> GetCurrentContractAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<DecisionContractDto?> GetContractByIdAsync(
        Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default);

    // Provisions a DRAFT v1 contract for a matter (pre-filled from matter context) if none exists.
    // Returns the current authoritative contract afterwards.
    Task<DecisionContractDto> ProvisionAsync(
        Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DecisionContractVersionSummaryDto>> GetVersionHistoryAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DecisionContractReviewDto>> GetReviewsAsync(
        Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DecisionContractOptionDto>> GetOptionsAsync(
        Guid tenantId, CancellationToken cancellationToken = default);

    Task<string?> GetMatterTitleAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    // Read-only POLOXI scoring weights (POLOXI.Legal_DecisionSetting) surfaced for transparency only.
    Task<IReadOnlyList<PoloxiWeightDto>> GetPoloxiWeightsAsync(
        CancellationToken cancellationToken = default);

    // Section updates — enforce optimistic concurrency via RowVersion; throw on conflict.
    Task UpdateDecisionAsync(Guid tenantId, Guid userId, DecisionContractDecisionCommand command, CancellationToken cancellationToken = default);
    Task UpdateLegalContextAsync(Guid tenantId, Guid userId, DecisionContractLegalContextCommand command, CancellationToken cancellationToken = default);
    Task UpdateBurdenAsync(Guid tenantId, Guid userId, DecisionContractBurdenCommand command, CancellationToken cancellationToken = default);
    Task UpdateBoundariesAsync(Guid tenantId, Guid userId, DecisionContractBoundariesCommand command, CancellationToken cancellationToken = default);
    Task UpdateCandidatesAsync(Guid tenantId, Guid userId, DecisionContractCandidatesCommand command, CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(Guid tenantId, Guid userId, DecisionContractSettingsCommand command, CancellationToken cancellationToken = default);

    // Lifecycle transitions — governed; enforce RowVersion + status guard; write review + audit rows.
    Task TransitionStatusAsync(
        Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion,
        string fromStatus, string toStatus, string reviewAction, string? comment,
        CancellationToken cancellationToken = default);

    // Creates a new DRAFT version cloned from an ACTIVE/APPROVED contract. Returns the new contract.
    Task<DecisionContractDto> CreateNewVersionAsync(
        Guid tenantId, Guid userId, Guid decisionContractId, CancellationToken cancellationToken = default);

    // Activates an APPROVED contract, superseding any prior ACTIVE version (transactional).
    Task ActivateAsync(
        Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion,
        CancellationToken cancellationToken = default);
}

// Raised when a section update or lifecycle transition loses the RowVersion optimistic-concurrency check.
public sealed class DecisionContractConcurrencyException(string message) : Exception(message);

// Raised when a lifecycle transition is invalid for the current status.
public sealed class DecisionContractStateException(string message) : Exception(message);
