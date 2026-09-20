using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Owner-only organization/tenant profile service.
//
// Backs the Account Settings surface (/account/settings) exposed only to the
// tenant OWNER. It reads and updates the tenant Name and Slug in SaaS.SaaS_Tenant,
// enforcing required fields, length limits (matching the DB columns), slug format,
// and the unique-slug constraint. All access/scope checks are performed by the
// controller against the authenticated tenant; this service is always tenant-scoped.
// ─────────────────────────────────────────────────────────────────────────────
public sealed partial class TenantProfileService(ISaasRepository repository) : ITenantProfileService
{
    public Task<TenantProfileDto?> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
        => repository.GetTenantProfileAsync(tenantId, ct);

    public async Task<TenantProfileDto> UpdateProfileAsync(Guid tenantId, Guid? actorUserId, UpdateTenantProfileRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name))
            throw new UserManagementForbiddenException("Organization name is required.");
        if (name.Length > 200)
            throw new UserManagementForbiddenException("Organization name must be 200 characters or fewer.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new UserManagementForbiddenException("Workspace slug is required.");
        if (slug.Length > 100)
            throw new UserManagementForbiddenException("Workspace slug must be 100 characters or fewer.");
        if (!SlugPattern().IsMatch(slug))
            throw new UserManagementForbiddenException("Workspace slug may contain only lowercase letters, numbers, and single hyphens.");

        var existing = await repository.GetTenantProfileAsync(tenantId, ct)
            ?? throw new UserManagementForbiddenException("Organization profile was not found.");

        if (await repository.TenantSlugExistsAsync(slug, tenantId, ct))
            throw new UserManagementForbiddenException("That workspace slug is already in use. Choose another.");

        await repository.UpdateTenantProfileAsync(tenantId, name, slug, actorUserId, ct);

        return existing with { Name = name, Slug = slug };
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
