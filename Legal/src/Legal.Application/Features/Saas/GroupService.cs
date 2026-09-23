using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Tenant group administration (Phase C).
//
// A group is a tenant-scoped collection that carries one or more platform roles.
// Adding a user to a group grants that group's roles (resolved additively at
// authorization time; the single-role TenantMembership invariant is preserved).
//
// All actions are tenant-scoped: a tenant admin can never reach into another
// tenant's groups. Role codes are resolved to the platform-catalog role ids so
// the database stays the source of truth.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GroupService(ISaasRepository repository) : IGroupService
{
    public Task<IReadOnlyList<TenantGroupDto>> ListGroupsAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        return repository.ListGroupsAsync(tenantId, ct);
    }

    public async Task<TenantGroupDetailDto?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        var group = await repository.GetGroupAsync(tenantId, groupId, ct);
        if (group is null)
            return null;

        var roles = await repository.GetGroupRolesAsync(groupId, ct);
        return new TenantGroupDetailDto(
            group.GroupId, group.TenantId, group.Name, group.Description, group.StatusCode,
            group.SortOrder, roles, group.MemberCount, group.CreatedDateUtc);
    }

    public async Task<TenantGroupDto> CreateGroupAsync(Guid tenantId, Guid? actorUserId, CreateGroupRequest request, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        var name = NormalizeName(request.Name);
        var roleIds = await ResolveRoleIdsAsync(request.RoleCodes, ct);

        var groupId = await repository.CreateGroupAsync(tenantId, name, NormalizeDescription(request.Description), actorUserId, roleIds, ct);
        return await repository.GetGroupAsync(tenantId, groupId, ct)
            ?? throw new InvalidOperationException("Group was created but could not be loaded.");
    }

    public async Task<TenantGroupDto> UpdateGroupAsync(Guid tenantId, Guid? actorUserId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        var existing = await repository.GetGroupAsync(tenantId, request.GroupId, ct)
            ?? throw new UserManagementForbiddenException("Group not found.");
        if (existing.TenantId != tenantId)
            throw new UserManagementForbiddenException("You may only manage groups within your own tenant.");

        var name = NormalizeName(request.Name);
        var status = NormalizeStatus(request.StatusCode);
        var roleIds = await ResolveRoleIdsAsync(request.RoleCodes, ct);

        await repository.UpdateGroupAsync(tenantId, request.GroupId, name, NormalizeDescription(request.Description), status, actorUserId, roleIds, ct);
        return await repository.GetGroupAsync(tenantId, request.GroupId, ct)
            ?? throw new InvalidOperationException("Group was updated but could not be loaded.");
    }

    public async Task DeleteGroupAsync(Guid tenantId, Guid? actorUserId, Guid groupId, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        var existing = await repository.GetGroupAsync(tenantId, groupId, ct)
            ?? throw new UserManagementForbiddenException("Group not found.");
        if (existing.TenantId != tenantId)
            throw new UserManagementForbiddenException("You may only manage groups within your own tenant.");

        await repository.DeleteGroupAsync(tenantId, groupId, actorUserId, ct);
    }

    public async Task<IReadOnlyList<GroupMemberDto>> ListGroupMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        await EnsureGroupInTenantAsync(tenantId, groupId, ct);
        return await repository.ListGroupMembersAsync(tenantId, groupId, ct);
    }

    public async Task AddGroupMemberAsync(Guid tenantId, Guid? actorUserId, Guid groupId, Guid userId, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        await EnsureGroupInTenantAsync(tenantId, groupId, ct);
        await repository.AddGroupMemberAsync(tenantId, groupId, userId, actorUserId, ct);
    }

    public async Task RemoveGroupMemberAsync(Guid tenantId, Guid? actorUserId, Guid groupId, Guid userId, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        await EnsureGroupInTenantAsync(tenantId, groupId, ct);
        await repository.RemoveGroupMemberAsync(tenantId, groupId, userId, actorUserId, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────
    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new UserManagementForbiddenException("A tenant scope is required.");
    }

    private async Task EnsureGroupInTenantAsync(Guid tenantId, Guid groupId, CancellationToken ct)
    {
        var group = await repository.GetGroupAsync(tenantId, groupId, ct)
            ?? throw new UserManagementForbiddenException("Group not found.");
        if (group.TenantId != tenantId)
            throw new UserManagementForbiddenException("You may only manage groups within your own tenant.");
    }

    private async Task<IReadOnlyList<Guid>> ResolveRoleIdsAsync(IReadOnlyList<string>? roleCodes, CancellationToken ct)
    {
        if (roleCodes is null || roleCodes.Count == 0)
            return [];
        var ids = new List<Guid>();
        foreach (var code in roleCodes)
        {
            if (string.IsNullOrWhiteSpace(code))
                continue;
            var id = await repository.GetRoleIdByCodeAsync(code, ct)
                ?? throw new InvalidOperationException($"Role '{code}' is not defined.");
            ids.Add(id);
        }
        return ids;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("A group name is required.");
        return name.Trim();
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string NormalizeStatus(string? statusCode)
    {
        if (string.Equals(statusCode, TenantGroupStatus.Inactive, StringComparison.OrdinalIgnoreCase))
            return TenantGroupStatus.Inactive;
        return TenantGroupStatus.Active;
    }
}
