using Legal.Application.Features.Saas;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Configuration data access abstraction.
//
// Dapper-backed access to SaaS.Config_Definition and the scoped value tables.
// Writes append to SaaS.Config_ChangeHistory. Secrets are never written to the
// value tables here (Sensitivity=Secret uses SecretReference), and history for
// sensitive keys stores [REDACTED] instead of the value.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A scoped configuration value row (platform/tenant/matter).</summary>
public sealed record ConfigurationValueRow(string ConfigKey, string? ValueJson);

/// <summary>A configuration change-history entry (freeze §24).</summary>
public sealed record ConfigurationHistoryRow(
    Guid ConfigChangeHistoryId,
    Guid? TenantId,
    Guid? MatterId,
    string ConfigKey,
    string Scope,
    string? OldValueJson,
    string? NewValueJson,
    Guid? ActorUserId,
    DateTime OccurredAtUtc);

public interface IConfigurationRepository
{
    // Definitions ----------------------------------------------------------------
    Task<IReadOnlyList<ConfigurationDefinition>> GetDefinitionsAsync(CancellationToken ct = default);
    Task<ConfigurationDefinition?> GetDefinitionAsync(string key, CancellationToken ct = default);

    // Scoped value reads ---------------------------------------------------------
    Task<ConfigurationValueRow?> GetPlatformValueAsync(string key, CancellationToken ct = default);
    Task<ConfigurationValueRow?> GetTenantValueAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task<ConfigurationValueRow?> GetMatterValueAsync(Guid tenantId, Guid matterId, string key, CancellationToken ct = default);

    // Scoped value writes (upsert) -----------------------------------------------
    Task UpsertPlatformValueAsync(string key, string? valueJson, Guid? actorUserId, CancellationToken ct = default);
    Task UpsertTenantValueAsync(Guid tenantId, string key, string? valueJson, Guid? actorUserId, CancellationToken ct = default);
    Task UpsertMatterValueAsync(Guid tenantId, Guid matterId, string key, string? valueJson, Guid? actorUserId, CancellationToken ct = default);

    // History --------------------------------------------------------------------
    Task AppendChangeHistoryAsync(ConfigurationHistoryRow row, string? correlationId, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigurationHistoryRow>> GetChangeHistoryAsync(Guid? tenantId, string? key, int take, CancellationToken ct = default);
}
