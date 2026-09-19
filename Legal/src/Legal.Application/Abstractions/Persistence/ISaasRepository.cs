using Legal.Application.Features.Saas;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — data access abstraction (Dapper-backed).
//
// One repository over the SaaS/Commerce/Platform tables. All reads/writes are
// tenant-scoped where applicable and honour soft-delete (IsDeleted = 0).
// ─────────────────────────────────────────────────────────────────────────────
public interface ISaasRepository
{
    // Email verification challenge -------------------------------------------------
    Task CreateVerificationChallengeAsync(Guid userId, string purpose, byte[] codeHash, DateTime expiresAtUtc, int maxAttempts, CancellationToken ct = default);
    Task<VerificationChallengeRow?> GetActiveChallengeAsync(Guid userId, string purpose, CancellationToken ct = default);
    Task IncrementChallengeAttemptAsync(Guid challengeId, CancellationToken ct = default);
    Task ConsumeChallengeAsync(Guid challengeId, CancellationToken ct = default);
    Task TouchChallengeResendAsync(Guid challengeId, DateTime lastSentAtUtc, CancellationToken ct = default);

    // Provisioning state ----------------------------------------------------------
    Task<string?> GetProvisioningStatusAsync(Guid userId, CancellationToken ct = default);
    Task UpsertProvisioningStatusAsync(Guid userId, string statusCode, Guid? tenantId, string? failureReason, CancellationToken ct = default);

    // Tenancy ---------------------------------------------------------------------
    Task<Guid> CreateTenantAsync(string name, string slug, Guid ownerUserId, CancellationToken ct = default);
    Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
    Task CreateMembershipAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default);
    Task CreateTenantPlacementAsync(Guid tenantId, string regionCode, CancellationToken ct = default);
    Task<TenantMembershipDto?> GetPrimaryMembershipAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken ct = default);
    // Additive authorization: primary membership role permissions UNION active group role permissions.
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    // User management -------------------------------------------------------------
    Task<IReadOnlyList<ManagedMemberDto>> ListMembersAsync(Guid? tenantId, CancellationToken ct = default);
    Task<ManagedMemberDto?> GetMemberAsync(Guid membershipId, CancellationToken ct = default);
    Task<IReadOnlyList<AssignableRoleDto>> ListAssignableRolesAsync(bool includeSystemRoles, CancellationToken ct = default);
    Task<IReadOnlyList<TenantOptionDto>> ListTenantsAsync(CancellationToken ct = default);
    Task<string?> GetTenantNameAsync(Guid tenantId, CancellationToken ct = default);
    Task<Guid?> FindUserIdByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<Guid?> FindActiveMembershipIdAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<int> CountActiveOwnersAsync(Guid tenantId, Guid excludeMembershipId, CancellationToken ct = default);
    Task<Guid> UpsertMembershipAsync(Guid tenantId, Guid userId, Guid roleId, Guid? actorUserId, CancellationToken ct = default);
    Task UpdateMembershipRoleAsync(Guid membershipId, Guid roleId, Guid? actorUserId, CancellationToken ct = default);
    Task UpdateMembershipStatusAsync(Guid membershipId, string statusCode, Guid? actorUserId, CancellationToken ct = default);
    Task RemoveMembershipAsync(Guid membershipId, Guid? actorUserId, CancellationToken ct = default);

    // Invitations (Phase B) -------------------------------------------------------
    Task<Guid> CreateInvitationAsync(Guid tenantId, string normalizedEmail, Guid roleId, byte[] tokenHash, DateTime expiresAtUtc, Guid? invitedByUserId, IReadOnlyList<Guid> additionalRoleIds, CancellationToken ct = default);
    Task<IReadOnlyList<TenantInvitationDto>> ListInvitationsAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantInvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken ct = default);
    Task<InvitationRow?> GetInvitationByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetInvitationRoleIdsAsync(Guid invitationId, CancellationToken ct = default);
    Task MarkInvitationAcceptedAsync(Guid invitationId, Guid acceptedByUserId, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid invitationId, Guid? actorUserId, CancellationToken ct = default);
    Task UpdateInvitationTokenAsync(Guid invitationId, byte[] tokenHash, DateTime expiresAtUtc, Guid? actorUserId, CancellationToken ct = default);

    // Transactional outbox --------------------------------------------------------
    Task<Guid> EnqueueOutboxAsync(string messageType, string payloadJson, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessageDto>> DequeueOutboxBatchAsync(int batchSize, CancellationToken ct = default);
    Task MarkOutboxSentAsync(Guid outboxId, CancellationToken ct = default);
    Task MarkOutboxFailedAsync(Guid outboxId, string error, DateTime nextAttemptUtc, CancellationToken ct = default);

    // Groups (Phase C) ------------------------------------------------------------
    Task<IReadOnlyList<TenantGroupDto>> ListGroupsAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantGroupDto?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<AssignableRoleDto>> GetGroupRolesAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetGroupRoleIdsAsync(Guid groupId, CancellationToken ct = default);
    Task<Guid> CreateGroupAsync(Guid tenantId, string name, string? description, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);
    Task UpdateGroupAsync(Guid tenantId, Guid groupId, string name, string? description, string statusCode, Guid? actorUserId, IReadOnlyList<Guid> roleIds, CancellationToken ct = default);
    Task DeleteGroupAsync(Guid tenantId, Guid groupId, Guid? actorUserId, CancellationToken ct = default);

    Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
    Task AddGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default);
    Task RemoveGroupMemberAsync(Guid tenantId, Guid groupId, Guid userId, Guid? actorUserId, CancellationToken ct = default);

    // Invitation groups (Phase C) -------------------------------------------------
    Task AddInvitationGroupsAsync(Guid invitationId, IReadOnlyList<Guid> groupIds, Guid? actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetInvitationGroupIdsAsync(Guid invitationId, CancellationToken ct = default);

    // Commerce --------------------------------------------------------------------
    Task<Guid?> GetPlanIdByCodeAsync(string planCode, CancellationToken ct = default);
    Task CreateSubscriptionAsync(Guid tenantId, Guid planId, CancellationToken ct = default);
    Task<Guid?> GetActiveSubscriptionPlanIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<EntitlementResolution>> ResolveEntitlementsAsync(Guid tenantId, CancellationToken ct = default);

    // Capability registry ---------------------------------------------------------
    Task<CapabilityDefinition?> GetCapabilityAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<CapabilityDefinition>> GetCapabilitiesAsync(CancellationToken ct = default);

    // Usage -----------------------------------------------------------------------
    Task<long> GetMonthlyUsageAsync(Guid tenantId, string meterCode, CancellationToken ct = default);
    Task RecordUsageAsync(Guid tenantId, Guid? userId, Guid? executionId, Guid? matterId, string meterCode, string usageClass, decimal quantity, string? correlationId, CancellationToken ct = default);
    Task<Guid> ReserveUsageAsync(Guid tenantId, Guid? executionId, string meterCode, decimal quantity, DateTime expiresAtUtc, CancellationToken ct = default);
    Task UpdateReservationStatusAsync(Guid reservationId, string statusCode, CancellationToken ct = default);

    // Execution + idempotency -----------------------------------------------------
    Task<Guid> CreateExecutionAsync(Guid tenantId, Guid userId, Guid? matterId, string capabilityCode, string correlationId, Guid? parentExecutionId, CancellationToken ct = default);
    Task UpdateExecutionStatusAsync(Guid executionId, string statusCode, string? failureCode, CancellationToken ct = default);
    Task<IntelligenceExecutionDto?> GetExecutionAsync(Guid tenantId, Guid executionId, CancellationToken ct = default);
    Task<Guid?> GetIdempotentExecutionAsync(Guid tenantId, string operation, string idempotencyKey, CancellationToken ct = default);
    Task CreateIdempotencyRecordAsync(Guid tenantId, string operation, string idempotencyKey, Guid executionId, DateTime expiresAtUtc, CancellationToken ct = default);

    // Audit -----------------------------------------------------------------------
    Task WriteAuditAsync(Guid? tenantId, Guid? userId, string eventType, Guid? executionId, string? resourceType, Guid? resourceId, string? dataJson, string? correlationId, CancellationToken ct = default);

    // Activity read surface (audit + usage + login history) -----------------------
    Task<IReadOnlyList<AuditEventDto>> ListAuditEventsAsync(Guid tenantId, DateTime sinceUtc, int take, CancellationToken ct = default);
    Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageAsync(Guid tenantId, DateTime sinceUtc, CancellationToken ct = default);
    Task RecordLoginAsync(RecordLoginRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LoginHistoryDto>> ListLoginHistoryAsync(Guid tenantId, DateTime sinceUtc, int take, CancellationToken ct = default);

    // Legal clickwrap consent -----------------------------------------------------
    Task<IReadOnlyList<LegalAgreementDto>> GetActiveAgreementsAsync(CancellationToken ct = default);
    Task RecordConsentAsync(RecordConsentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecordDto>> ListConsentRecordsForUserAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}

public sealed record VerificationChallengeRow(
    Guid ChallengeId,
    Guid UserId,
    byte[] CodeHash,
    DateTime ExpiresAtUtc,
    int AttemptCount,
    int MaxAttempts,
    DateTime? ConsumedAtUtc);

/// <summary>Internal invitation row including sensitive lifecycle fields (service use only).</summary>
public sealed record InvitationRow(
    Guid InvitationId,
    Guid TenantId,
    string EmailNormalized,
    Guid RoleId,
    string StatusCode,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc);