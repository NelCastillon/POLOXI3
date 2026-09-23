using Legal.Application.Features.Saas;

namespace Legal.Application.Abstractions.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Configuration resolver (freeze §21/§22).
//
// Resolves the effective value of a configuration key across the scope hierarchy
//   Execution → Matter → Tenant → Plan → Platform → Code default
// and enforces the definition's override policy. When a definition sets
// TenantCanOverride = false the platform value is authoritative and lower scopes
// are ignored. Configuration NEVER grants access — it only shapes how a permitted
// capability behaves.
// ─────────────────────────────────────────────────────────────────────────────

public interface IConfigurationResolver
{
    /// <summary>Resolve the effective typed value of <paramref name="key"/> for the given context.</summary>
    Task<ResolvedConfiguration<T>> ResolveAsync<T>(string key, ConfigurationContext context, CancellationToken ct = default);

    /// <summary>List every configuration definition (registry).</summary>
    Task<IReadOnlyList<ConfigurationDefinition>> GetDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Write a scoped configuration value, honouring scope/override rules, recording history.</summary>
    Task SetTenantValueAsync(string key, string? valueJson, ConfigurationContext context, CancellationToken ct = default);
    Task SetPlatformValueAsync(string key, string? valueJson, ConfigurationContext context, CancellationToken ct = default);
    Task SetMatterValueAsync(string key, string? valueJson, ConfigurationContext context, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Tenant admin read model — assembles the DB-backed registry into UI items with
// effective value + provenance for a specific tenant (/admin/configuration).
// ─────────────────────────────────────────────────────────────────────────────
public interface ITenantConfigurationService
{
    /// <summary>List tenant-configurable definitions grouped by category, each with its effective value.</summary>
    Task<IReadOnlyList<TenantConfigurationCategoryDto>> GetForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Set or clear (valueJson = null) a tenant-scope value; runs validation + history + audit.</summary>
    Task SetTenantValueAsync(Guid tenantId, Guid? actorUserId, string key, string? valueJson, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Platform admin read model — assembles the full DB-backed registry into UI items
// with the effective platform value + provenance (/platform/configuration).
// Super Admin only; editing here sets the platform default all tenants inherit.
// ─────────────────────────────────────────────────────────────────────────────
public interface IPlatformConfigurationService
{
    /// <summary>List every configuration definition grouped by category, each with its effective platform value.</summary>
    Task<IReadOnlyList<PlatformConfigurationCategoryDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Set or clear (valueJson = null) a platform-scope value; runs validation + history + audit.</summary>
    Task SetPlatformValueAsync(Guid? actorUserId, string key, string? valueJson, CancellationToken ct = default);
}