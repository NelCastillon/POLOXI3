using Legal.Application.Features.Saas;

namespace Legal.Application.Abstractions.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — application service abstractions.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Sends transactional emails (verification codes, password reset).</summary>
public interface IJudzEmailSender
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default);
    Task SendInvitationAsync(string email, string tenantName, string roleName, string acceptUrl, CancellationToken ct = default);
}

/// <summary>Generates + validates single-use, time-limited 6-digit verification codes.</summary>
public interface IEmailVerificationService
{
    Task IssueCodeAsync(Guid userId, string email, CancellationToken ct = default);
    Task<bool> ResendCodeAsync(Guid userId, string email, CancellationToken ct = default);
    Task<EmailVerificationOutcome> VerifyCodeAsync(Guid userId, string code, CancellationToken ct = default);
}

public enum EmailVerificationOutcome
{
    Success,
    InvalidCode,
    Expired,
    NoActiveChallenge,
    TooManyAttempts
}

/// <summary>Idempotent workspace provisioning after email verification.</summary>
public interface IWorkspaceProvisioningService
{
    Task<Guid> ProvisionAsync(Guid userId, string email, string? displayName, CancellationToken ct = default);
}

/// <summary>Resolves the authoritative active tenant + permissions for a user.</summary>
public interface ITenantContextService
{
    Task<TenantMembershipDto?> ResolveMembershipAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ResolvePermissionsAsync(Guid roleId, CancellationToken ct = default);
    // Additive: primary membership role permissions UNION active group role permissions for the tenant.
    Task<IReadOnlyList<string>> ResolveEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}

/// <summary>Resolves effective entitlements for a tenant (plan + overrides).</summary>
public interface IEntitlementService
{
    Task<IReadOnlyList<EntitlementResolution>> GetEntitlementsAsync(Guid tenantId, CancellationToken ct = default);
    Task<EntitlementResolution?> GetEntitlementAsync(Guid tenantId, string code, CancellationToken ct = default);
}

/// <summary>Answers "are you allowed?" — authentication, membership, permission, entitlement, matter access.</summary>
public interface ICapabilityAuthorizationService
{
    Task<CapabilityAuthorizationResult> AuthorizeAsync(Guid userId, Guid tenantId, string capabilityCode, Guid? matterId, IReadOnlyCollection<string> permissions, CancellationToken ct = default);
}

/// <summary>Answers "do you have capacity?" and provides atomic reservation.</summary>
public interface IUsageService
{
    Task<UsageCheckResult> CheckAsync(Guid tenantId, string meterCode, string limitEntitlementCode, CancellationToken ct = default);
    Task<Guid> ReserveAsync(Guid tenantId, Guid? executionId, string meterCode, CancellationToken ct = default);
    Task CommitAsync(Guid reservationId, Guid tenantId, Guid? userId, Guid? executionId, Guid? matterId, string meterCode, string? correlationId, CancellationToken ct = default);
    Task ReleaseAsync(Guid reservationId, CancellationToken ct = default);
}

/// <summary>The single execution boundary: authorize → reserve → idempotency → create execution.</summary>
public interface IIntelligenceExecutionService
{
    Task<IntelligenceExecutionDto> CreateAsync(Guid userId, Guid tenantId, IReadOnlyCollection<string> permissions, ExecuteCapabilityRequest request, CancellationToken ct = default);
    Task<IntelligenceExecutionDto?> GetAsync(Guid tenantId, Guid executionId, CancellationToken ct = default);
}

public sealed class CapabilityAuthorizationException(string reason) : Exception(reason);
public sealed class UsageQuotaExceededException(string meterCode, long limit) : Exception($"Monthly quota exceeded for {meterCode} (limit {limit}).")
{
    public string MeterCode { get; } = meterCode;
    public long Limit { get; } = limit;
}

/// <summary>Creates or locates the underlying identity account (password-hashed) for provisioning.</summary>
public interface IUserAccountCreator
{
    /// <summary>Create an identity user with the supplied temporary password. Returns the new user id.</summary>
    Task<Guid> CreateAsync(string email, string firstName, string lastName, string temporaryPassword, bool emailConfirmed, CancellationToken ct = default);

    /// <summary>Create an invited identity user (unusable password) and issue a verification code. Returns the new user id.</summary>
    Task<Guid> InviteAsync(string email, string firstName, string lastName, CancellationToken ct = default);

    /// <summary>Admin-set a new (temporary) password for an existing identity user, replacing any current password.</summary>
    Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
}

/// <summary>Scope-aware member administration for Super Admin (platform) and Tenant Admin surfaces.</summary>
public interface IUserManagementService
{
    /// <summary>List members. When <paramref name="scopeTenantId"/> is null the caller must be platform scope (all tenants).</summary>
    Task<IReadOnlyList<ManagedMemberDto>> ListMembersAsync(Guid? scopeTenantId, CancellationToken ct = default);

    /// <summary>Assignable roles for the given scope (platform includes SUPERADMIN; tenant excludes it).</summary>
    Task<IReadOnlyList<AssignableRoleDto>> ListAssignableRolesAsync(bool platformScope, CancellationToken ct = default);

    /// <summary>Tenant options (platform scope only) for the tenant selector.</summary>
    Task<IReadOnlyList<TenantOptionDto>> ListTenantsAsync(CancellationToken ct = default);

    Task<ProvisionMemberResult> InviteMemberAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, InviteMemberRequest request, CancellationToken ct = default);
    Task<ProvisionMemberResult> CreateMemberAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, CreateMemberRequest request, CancellationToken ct = default);
    Task ChangeRoleAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, ChangeMemberRoleRequest request, CancellationToken ct = default);
    Task ChangeStatusAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, ChangeMemberStatusRequest request, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, Guid membershipId, CancellationToken ct = default);
    Task ResetLockoutAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, Guid membershipId, CancellationToken ct = default);
    Task SetMemberPasswordAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, SetMemberPasswordRequest request, CancellationToken ct = default);
}
public sealed class UserManagementForbiddenException(string reason) : Exception(reason);

/// <summary>Owner-only organization/tenant profile management (view &amp; edit name/slug).</summary>
public interface ITenantProfileService
{
    Task<TenantProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantProfileDto> UpdateProfileAsync(Guid tenantId, Guid? actorUserId, UpdateTenantProfileRequest request, CancellationToken ct = default);
}

/// <summary>Tenant invitation lifecycle: create (with pending roles) + email via outbox, list, resend, revoke, accept.</summary>
public interface IInvitationService
{
    Task<IReadOnlyList<TenantInvitationDto>> ListInvitationsAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantInvitationDto> CreateInvitationAsync(Guid tenantId, Guid? actorUserId, CreateInvitationRequest request, CancellationToken ct = default);
    Task ResendInvitationAsync(Guid tenantId, Guid? actorUserId, Guid invitationId, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid tenantId, Guid? actorUserId, Guid invitationId, CancellationToken ct = default);

    /// <summary>Public: resolve an invitation by its raw token for the accept page (no sensitive data).</summary>
    Task<InvitationLookupDto?> LookupByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Public: accept an invitation by raw token, provisioning membership + pending roles idempotently.</summary>
    Task<AcceptInvitationResult> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default);
}

/// <summary>Drains queued transactional emails from the outbox and dispatches them via IJudzEmailSender.</summary>
public interface IOutboxDispatcher
{
    /// <summary>Process a single batch of pending outbox messages. Returns the number processed.</summary>
    Task<int> ProcessBatchAsync(int batchSize, CancellationToken ct = default);
}

/// <summary>Tenant-scoped group administration: CRUD, group→role mapping, and membership (Phase C).</summary>
public interface IGroupService
{
    Task<IReadOnlyList<TenantGroupDto>> ListGroupsAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantGroupDetailDto?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
    Task<TenantGroupDto> CreateGroupAsync(Guid tenantId, Guid? actorUserId, CreateGroupRequest request, CancellationToken ct = default);
    Task<TenantGroupDto> UpdateGroupAsync(Guid tenantId, Guid? actorUserId, UpdateGroupRequest request, CancellationToken ct = default);
    Task DeleteGroupAsync(Guid tenantId, Guid? actorUserId, Guid groupId, CancellationToken ct = default);

    Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default);
    Task AddGroupMemberAsync(Guid tenantId, Guid? actorUserId, Guid groupId, Guid userId, CancellationToken ct = default);
    Task RemoveGroupMemberAsync(Guid tenantId, Guid? actorUserId, Guid groupId, Guid userId, CancellationToken ct = default);
}

/// <summary>Tenant-scoped read surface over audit events, usage totals, and login history.</summary>
public interface IActivityService
{
    Task<IReadOnlyList<AuditEventDto>> ListAuditEventsAsync(Guid tenantId, int days, int take, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEventDto>> ListAuditEventsForUserAsync(Guid tenantId, Guid userId, int days, int take, CancellationToken ct = default);
    Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageAsync(Guid tenantId, int days, CancellationToken ct = default);
    Task<IReadOnlyList<UsageSummaryDto>> SummarizeUsageForUserAsync(Guid tenantId, Guid userId, int days, CancellationToken ct = default);
    Task<IReadOnlyList<LoginHistoryDto>> ListLoginHistoryAsync(Guid tenantId, int days, int take, CancellationToken ct = default);

    /// <summary>Best-effort append of a login attempt; never throws to the caller.</summary>
    Task RecordLoginAsync(RecordLoginRequest request, CancellationToken ct = default);
}

/// <summary>Serves DB-backed legal agreements and records clickwrap consent evidence.</summary>
public interface IConsentService
{
    /// <summary>Returns the currently active agreements (Terms, Privacy) users must accept.</summary>
    Task<IReadOnlyList<LegalAgreementDto>> GetActiveAgreementsAsync(CancellationToken ct = default);

    /// <summary>Records acceptance of every active agreement for a user as legal evidence.</summary>
    Task RecordConsentForActiveAgreementsAsync(
        Guid? userId,
        Guid? tenantId,
        string? email,
        string acceptanceMethod,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Returns the recorded consent evidence for a user within a tenant.</summary>
    Task<IReadOnlyList<ConsentRecordDto>> GetConsentHistoryAsync(Guid userId, Guid? tenantId, CancellationToken ct = default);
}