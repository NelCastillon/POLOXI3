using Legal.Application.Features.ProviderPortal;

namespace Legal.Application.Abstractions.Persistence;

/// <summary>
/// DB-backed persistence for the provider portal: attorney sharing policies,
/// firm→provider requests, and per matter+user review state. Tenant-scoped.
/// </summary>
public interface IProviderPortalRepository
{
    // ── Sharing policy ──────────────────────────────────────────────────────
    Task<IReadOnlyCollection<ProviderSharingPolicyDto>> GetSharingPoliciesAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    Task<ProviderSharingPolicyDto?> GetSharingPolicyAsync(
        Guid tenantId,
        Guid matterId,
        string providerKey,
        CancellationToken cancellationToken = default);

    Task<ProviderSharingPolicyDto> UpsertSharingPolicyAsync(
        Guid tenantId,
        Guid userId,
        SaveProviderSharingPolicyRequest request,
        CancellationToken cancellationToken = default);

    // ── Firm → provider requests ────────────────────────────────────────────
    Task<IReadOnlyCollection<ProviderRequestDto>> GetRequestsAsync(
        Guid tenantId,
        Guid matterId,
        string? providerKey,
        CancellationToken cancellationToken = default);

    Task<ProviderRequestDto> CreateRequestAsync(
        Guid tenantId,
        Guid userId,
        CreateProviderRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderRequestDto?> UpdateRequestStatusAsync(
        Guid tenantId,
        Guid userId,
        UpdateProviderRequestStatusRequest request,
        CancellationToken cancellationToken = default);

    // ── Review state ────────────────────────────────────────────────────────
    Task<MatterReviewStateDto?> GetReviewStateAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new "opened" moment for (matter, user): advances LastOpenedUtc
    /// and preserves the prior value in PreviousOpenedUtc. Returns the state as
    /// it was BEFORE this call (so the caller can diff against the prior boundary).
    /// </summary>
    Task<MatterReviewStateDto?> RecordMatterOpenedAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        CancellationToken cancellationToken = default);
}
