namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Application contracts (DTOs / records).
//
// Provider-agnostic data shapes used by the SaaS services, repositories and API
// controllers. All operational/reference values (plans, entitlements, limits,
// capabilities, statuses) originate from the database seeds, never hardcoded here.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record TenantDto(
    Guid TenantId,
    string Name,
    string Slug,
    string StatusCode,
    Guid? CreatedByUserId);

public sealed record TenantMembershipDto(
    Guid MembershipId,
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    string RoleCode,
    string StatusCode);

public sealed record ProductPlanDto(
    Guid PlanId,
    string Code,
    string Name,
    decimal PriceAmount,
    string CurrencyCode,
    string BillingPeriodCode,
    bool IsPublic,
    bool IsActive);

public sealed record CapabilityDefinition(
    string Code,
    string DisplayName,
    string EntitlementCode,
    string? Permission,
    string? CustomerMeterCode,
    bool RequiresMatter,
    bool IsMetered,
    bool IsAsync);

/// <summary>Effective entitlement for a tenant: plan grant merged with tenant override.</summary>
public sealed record EntitlementResolution(
    string Code,
    string Kind,
    bool IsEnabled,
    long? LimitValue,
    string? LimitPeriodCode);

public sealed record CapabilityAuthorizationResult(
    bool Allowed,
    string? DenialReason,
    CapabilityDefinition? Capability)
{
    public static CapabilityAuthorizationResult Allow(CapabilityDefinition capability)
        => new(true, null, capability);

    public static CapabilityAuthorizationResult Deny(string reason)
        => new(false, reason, null);
}

/// <summary>Result of a usage/quota check for a metered capability.</summary>
public sealed record UsageCheckResult(
    bool HasCapacity,
    long Limit,
    long Used,
    string MeterCode);

public sealed record IntelligenceExecutionDto(
    Guid ExecutionId,
    Guid TenantId,
    Guid RequestedByUserId,
    Guid? MatterId,
    string CapabilityCode,
    string StatusCode,
    string CorrelationId,
    string? FailureCode,
    DateTimeOffset CreatedDateUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    Guid? ParentExecutionId);

// ── Request contracts ────────────────────────────────────────────────────────

public sealed record SignupRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password);

public sealed record VerifyEmailRequest(string Email, string Code);

public sealed record ResendVerificationRequest(string Email);

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record ExecuteCapabilityRequest(
    string CapabilityCode,
    Guid? MatterId,
    string? IdempotencyKey,
    Guid? ParentExecutionId = null);

// ── User management (Super Admin /platform/users, Tenant Admin /admin/users) ──

/// <summary>A tenant membership joined with the user and (for platform scope) the tenant.</summary>
public sealed record ManagedMemberDto(
    Guid MembershipId,
    Guid UserId,
    Guid TenantId,
    string TenantName,
    string FirstName,
    string LastName,
    string Email,
    bool EmailConfirmed,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    string StatusCode,
    DateTime JoinedAtUtc,
    int AuthorizationVersion,
    string ProvisioningSource);

/// <summary>An assignable role from the platform (TenantId NULL) catalog.</summary>
public sealed record AssignableRoleDto(
    Guid RoleId,
    string Code,
    string DisplayName,
    int SortOrder);

/// <summary>A tenant option for the Super Admin scope selector.</summary>
public sealed record TenantOptionDto(
    Guid TenantId,
    string Name,
    string Slug,
    string StatusCode);

// ── User management request contracts ────────────────────────────────────────

public sealed record InviteMemberRequest(
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    string RoleCode);

public sealed record CreateMemberRequest(
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    string RoleCode,
    string TemporaryPassword);

public sealed record ChangeMemberRoleRequest(
    Guid MembershipId,
    string RoleCode);

public sealed record ChangeMemberStatusRequest(
    Guid MembershipId,
    string StatusCode);

/// <summary>Result of an invite/create provisioning action.</summary>
public sealed record ProvisionMemberResult(
    Guid UserId,
    Guid MembershipId,
    bool UserAlreadyExisted,
    string Email);

// ── Invitations (Phase B: tenant invitations + pending role assignments) ──────

/// <summary>Invitation lifecycle status codes (DB-authoritative).</summary>
public static class InvitationStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Expired = "Expired";
    public const string Revoked = "Revoked";
}

/// <summary>An invitation as shown on the tenant admin management surface.</summary>
public sealed record TenantInvitationDto(
    Guid InvitationId,
    Guid TenantId,
    string Email,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    string StatusCode,
    Guid? InvitedByUserId,
    DateTime ExpiresAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RevokedAtUtc,
    DateTime CreatedDateUtc);

/// <summary>Public, non-sensitive view of an invitation resolved by token for the accept page.</summary>
public sealed record InvitationLookupDto(
    Guid InvitationId,
    Guid TenantId,
    string TenantName,
    string Email,
    string RoleName,
    string StatusCode,
    DateTime ExpiresAtUtc,
    bool RequiresAccountCreation);

/// <summary>Create a tenant invitation with one primary role plus optional additional pending roles.</summary>
public sealed record CreateInvitationRequest(
    Guid TenantId,
    string Email,
    string RoleCode,
    IReadOnlyList<string>? AdditionalRoleCodes = null,
    IReadOnlyList<Guid>? GroupIds = null);

/// <summary>Accept an invitation by token. New users must supply name + password; existing users may omit them.</summary>
public sealed record AcceptInvitationRequest(
    string Token,
    string? FirstName,
    string? LastName,
    string? Password);

/// <summary>Outcome codes for an acceptance attempt.</summary>
public enum InvitationAcceptOutcome
{
    Success,
    InvalidToken,
    Expired,
    Revoked,
    AlreadyAccepted,
    AccountDetailsRequired,
    AlreadyMember
}

/// <summary>Result of accepting an invitation.</summary>
public sealed record AcceptInvitationResult(
    InvitationAcceptOutcome Outcome,
    Guid? TenantId,
    Guid? MembershipId,
    Guid? UserId,
    string? Message);

/// <summary>Row persisted to and drained from the transactional outbox.</summary>
public sealed record OutboxMessageDto(
    Guid OutboxId,
    string MessageType,
    string PayloadJson,
    int AttemptCount,
    int MaxAttempts);

/// <summary>Payload for an invitation email queued through the outbox.</summary>
public sealed record InvitationEmailPayload(
    string Email,
    string TenantName,
    string RoleName,
    string AcceptUrl);

// ── Groups (Phase C: tenant groups, group→role mappings, group membership) ────

/// <summary>Tenant group lifecycle status codes (DB-authoritative).</summary>
public static class TenantGroupStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}

/// <summary>A tenant-scoped group as shown on the group management surface.</summary>
public sealed record TenantGroupDto(
    Guid GroupId,
    Guid TenantId,
    string Name,
    string? Description,
    string StatusCode,
    int SortOrder,
    int RoleCount,
    int MemberCount,
    DateTime CreatedDateUtc);

/// <summary>A group with its assigned roles (ids + display names) for the edit surface.</summary>
public sealed record TenantGroupDetailDto(
    Guid GroupId,
    Guid TenantId,
    string Name,
    string? Description,
    string StatusCode,
    int SortOrder,
    IReadOnlyList<AssignableRoleDto> Roles,
    int MemberCount,
    DateTime CreatedDateUtc);

/// <summary>A member of a group joined with the user identity.</summary>
public sealed record GroupMemberDto(
    Guid GroupMemberId,
    Guid GroupId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime CreatedDateUtc);

/// <summary>Create a tenant group with an optional initial set of role codes.</summary>
public sealed record CreateGroupRequest(
    string Name,
    string? Description,
    IReadOnlyList<string>? RoleCodes = null);

/// <summary>Update a tenant group's name/description/status and its role set.</summary>
public sealed record UpdateGroupRequest(
    Guid GroupId,
    string Name,
    string? Description,
    string StatusCode,
    IReadOnlyList<string>? RoleCodes = null);

