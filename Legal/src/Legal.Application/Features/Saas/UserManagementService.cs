using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Scope-aware user management.
//
// Two surfaces share this service:
//   • Super Admin (platformScope = true, scopeTenantId = null): may manage members
//     across ANY tenant and grant ANY role, including the SUPERADMIN system role.
//   • Tenant Admin (platformScope = false, scopeTenantId = the caller's tenant):
//     may manage members ONLY within their own tenant and grant ONLY the
//     non-privileged tenant roles (OWNER/ADMIN/MEMBER/VIEWER). The service NEVER
//     lets a tenant admin escalate to SUPERADMIN or reach into another tenant.
//
// All privilege/scope checks are enforced here (server-side), not in the UI.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class UserManagementService(
    ISaasRepository repository,
    IUserAccountCreator accountCreator) : IUserManagementService
{
    // Roles a Tenant Admin is allowed to grant. SUPERADMIN is intentionally absent.
    private static readonly HashSet<string> TenantGrantableRoles =
        new(StringComparer.OrdinalIgnoreCase) { "OWNER", "ADMIN", "MEMBER", "VIEWER" };

    private static readonly HashSet<string> ValidStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Active", "Suspended", "Disabled" };

    // Statuses that revoke a member's active access (used for last-owner protection).
    private static readonly HashSet<string> DeactivatingStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Suspended", "Disabled" };

    public Task<IReadOnlyList<ManagedMemberDto>> ListMembersAsync(Guid? scopeTenantId, CancellationToken ct = default)
        => repository.ListMembersAsync(scopeTenantId, ct);

    public Task<IReadOnlyList<AssignableRoleDto>> ListAssignableRolesAsync(bool platformScope, CancellationToken ct = default)
        => repository.ListAssignableRolesAsync(includeSystemRoles: platformScope, ct);

    public Task<IReadOnlyList<TenantOptionDto>> ListTenantsAsync(CancellationToken ct = default)
        => repository.ListTenantsAsync(ct);

    public async Task<ProvisionMemberResult> InviteMemberAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, InviteMemberRequest request, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantScope(scopeTenantId, platformScope, request.TenantId);
        var roleId = await ResolveGrantableRoleIdAsync(platformScope, request.RoleCode, ct);
        var email = NormalizeEmail(request.Email);

        var (userId, existed) = await EnsureUserAsync(
            email,
            request.FirstName,
            request.LastName,
            temporaryPassword: null,
            emailConfirmed: false,
            ct);

        var membershipId = await repository.UpsertMembershipAsync(tenantId, userId, roleId, actorUserId, ct);
        await repository.WriteAuditAsync(tenantId, actorUserId, "MEMBER_INVITED", null, "TenantMembership", membershipId, null, null, ct);
        return new ProvisionMemberResult(userId, membershipId, existed, email);
    }

    public async Task<ProvisionMemberResult> CreateMemberAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, CreateMemberRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            throw new InvalidOperationException("A temporary password is required.");

        var tenantId = ResolveTenantScope(scopeTenantId, platformScope, request.TenantId);
        var roleId = await ResolveGrantableRoleIdAsync(platformScope, request.RoleCode, ct);
        var email = NormalizeEmail(request.Email);

        var (userId, existed) = await EnsureUserAsync(
            email,
            request.FirstName,
            request.LastName,
            request.TemporaryPassword,
            emailConfirmed: true,
            ct);

        var membershipId = await repository.UpsertMembershipAsync(tenantId, userId, roleId, actorUserId, ct);
        await repository.WriteAuditAsync(tenantId, actorUserId, "MEMBER_CREATED", null, "TenantMembership", membershipId, null, null, ct);
        return new ProvisionMemberResult(userId, membershipId, existed, email);
    }

    public async Task ChangeRoleAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, ChangeMemberRoleRequest request, CancellationToken ct = default)
    {
        var member = await LoadMemberInScopeAsync(scopeTenantId, platformScope, request.MembershipId, ct);
        var roleId = await ResolveGrantableRoleIdAsync(platformScope, request.RoleCode, ct);
        await repository.UpdateMembershipRoleAsync(member.MembershipId, roleId, actorUserId, ct);
        await repository.WriteAuditAsync(member.TenantId, actorUserId, "MEMBER_ROLE_CHANGED", null, "TenantMembership", member.MembershipId, null, null, ct);
    }

    public async Task ChangeStatusAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, ChangeMemberStatusRequest request, CancellationToken ct = default)
    {
        if (!ValidStatuses.Contains(request.StatusCode))
            throw new InvalidOperationException($"Unsupported status '{request.StatusCode}'.");

        var member = await LoadMemberInScopeAsync(scopeTenantId, platformScope, request.MembershipId, ct);
        // A tenant admin may not change the status of a SUPERADMIN membership.
        GuardPrivilegedTarget(platformScope, member);
        // Never deactivate the last active Owner of a tenant.
        if (DeactivatingStatuses.Contains(request.StatusCode))
            await GuardLastOwnerAsync(member, ct);
        await repository.UpdateMembershipStatusAsync(member.MembershipId, request.StatusCode, actorUserId, ct);
        await repository.WriteAuditAsync(member.TenantId, actorUserId, "MEMBER_STATUS_CHANGED", null, "TenantMembership", member.MembershipId, null, null, ct);
    }

    public async Task RemoveMemberAsync(Guid? scopeTenantId, bool platformScope, Guid? actorUserId, Guid membershipId, CancellationToken ct = default)
    {
        var member = await LoadMemberInScopeAsync(scopeTenantId, platformScope, membershipId, ct);
        GuardPrivilegedTarget(platformScope, member);
        // Never remove the last active Owner of a tenant.
        await GuardLastOwnerAsync(member, ct);
        await repository.RemoveMembershipAsync(member.MembershipId, actorUserId, ct);
        await repository.WriteAuditAsync(member.TenantId, actorUserId, "MEMBER_REMOVED", null, "TenantMembership", member.MembershipId, null, null, ct);
    }

    // Prevents orphaning a tenant by deactivating/removing its only remaining active Owner.
    private async Task GuardLastOwnerAsync(ManagedMemberDto member, CancellationToken ct)
    {
        if (!string.Equals(member.RoleCode, "OWNER", StringComparison.OrdinalIgnoreCase))
            return;
        if (!string.Equals(member.StatusCode, "Active", StringComparison.OrdinalIgnoreCase))
            return;
        var otherOwners = await repository.CountActiveOwnersAsync(member.TenantId, member.MembershipId, ct);
        if (otherOwners == 0)
            throw new UserManagementForbiddenException(
                "This is the tenant's last active Owner. Assign another Owner before disabling or removing this member.");
    }

    // ── Helpers
    private async Task<(Guid UserId, bool Existed)> EnsureUserAsync(string email, string firstName, string lastName, string? temporaryPassword, bool emailConfirmed, CancellationToken ct)
    {
        var existingId = await repository.FindUserIdByEmailAsync(email.ToUpperInvariant(), ct);
        if (existingId is Guid found)
            return (found, true);

        var userId = temporaryPassword is null
            ? await accountCreator.InviteAsync(email, firstName, lastName, ct)
            : await accountCreator.CreateAsync(email, firstName, lastName, temporaryPassword, emailConfirmed, ct);
        return (userId, false);
    }

    private async Task<ManagedMemberDto> LoadMemberInScopeAsync(Guid? scopeTenantId, bool platformScope, Guid membershipId, CancellationToken ct)
    {
        var member = await repository.GetMemberAsync(membershipId, ct)
            ?? throw new UserManagementForbiddenException("Membership not found.");

        if (!platformScope)
        {
            if (scopeTenantId is null)
                throw new UserManagementForbiddenException("A tenant scope is required for tenant administration.");
            if (member.TenantId != scopeTenantId.Value)
                throw new UserManagementForbiddenException("You may only manage members within your own tenant.");
        }

        return member;
    }

    private static void GuardPrivilegedTarget(bool platformScope, ManagedMemberDto member)
    {
        if (!platformScope && string.Equals(member.RoleCode, "SUPERADMIN", StringComparison.OrdinalIgnoreCase))
            throw new UserManagementForbiddenException("You may not manage a platform super administrator.");
    }

    private static Guid ResolveTenantScope(Guid? scopeTenantId, bool platformScope, Guid requestedTenantId)
    {
        if (platformScope)
        {
            if (requestedTenantId == Guid.Empty)
                throw new UserManagementForbiddenException("A target tenant is required.");
            return requestedTenantId;
        }

        if (scopeTenantId is null || scopeTenantId.Value == Guid.Empty)
            throw new UserManagementForbiddenException("A tenant scope is required for tenant administration.");

        // Tenant admins can only act within their own tenant regardless of the request payload.
        if (requestedTenantId != Guid.Empty && requestedTenantId != scopeTenantId.Value)
            throw new UserManagementForbiddenException("You may only manage members within your own tenant.");

        return scopeTenantId.Value;
    }

    private async Task<Guid> ResolveGrantableRoleIdAsync(bool platformScope, string roleCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
            throw new InvalidOperationException("A role is required.");

        if (!platformScope && !TenantGrantableRoles.Contains(roleCode))
            throw new UserManagementForbiddenException($"You are not allowed to grant the '{roleCode}' role.");

        return await repository.GetRoleIdByCodeAsync(roleCode, ct)
            ?? throw new InvalidOperationException($"Role '{roleCode}' is not defined.");
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("An email address is required.");
        return email.Trim();
    }
}
