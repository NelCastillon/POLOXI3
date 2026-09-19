using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using System.Text.Json;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Configuration resolver (freeze §21/§22).
//
// Effective value precedence (first applicable wins):
//   Execution → Matter → Tenant → Plan → Platform → Code default
// Plan scope has no seeded values yet, so it is skipped structurally until a
// plan-value table exists. When a definition sets TenantCanOverride = false the
// platform value (or code default) is authoritative and lower scopes are ignored.
//
// Writes validate scope + range/options, upsert the scoped value, append a
// Config_ChangeHistory row (secrets redacted), and emit a CONFIGURATION_CHANGED
// audit event. Configuration NEVER grants access.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ConfigurationResolver(
    IConfigurationRepository repository,
    ISaasRepository saasRepository) : IConfigurationResolver
{
    private const string Redacted = "[REDACTED]";

    public async Task<ResolvedConfiguration<T>> ResolveAsync<T>(string key, ConfigurationContext context, CancellationToken ct = default)
    {
        var definition = await repository.GetDefinitionAsync(key, ct)
            ?? throw new InvalidOperationException($"Configuration key '{key}' is not defined.");

        var canOverride = definition.TenantCanOverride;

        // Execution → Matter → Tenant (only honoured when override is permitted).
        if (canOverride)
        {
            if (definition.MatterConfigurable && context is { TenantId: { } tid, MatterId: { } mid })
            {
                var matterValue = await repository.GetMatterValueAsync(tid, mid, key, ct);
                if (matterValue?.ValueJson is { } mjson)
                    return Resolved<T>(mjson, ConfigurationScope.Matter, overridden: true, canOverride);
            }

            if (definition.TenantConfigurable && context.TenantId is { } tenantId)
            {
                var tenantValue = await repository.GetTenantValueAsync(tenantId, key, ct);
                if (tenantValue?.ValueJson is { } tjson)
                    return Resolved<T>(tjson, ConfigurationScope.Tenant, overridden: true, canOverride);
            }
        }

        // Platform value.
        var platformValue = await repository.GetPlatformValueAsync(key, ct);
        if (platformValue?.ValueJson is { } pjson)
            return Resolved<T>(pjson, ConfigurationScope.Platform, overridden: false, canOverride);

        // Code default.
        return Resolved<T>(definition.DefaultValueJson, ConfigurationScope.CodeDefault, overridden: false, canOverride);
    }

    public Task<IReadOnlyList<ConfigurationDefinition>> GetDefinitionsAsync(CancellationToken ct = default)
        => repository.GetDefinitionsAsync(ct);

    public async Task SetTenantValueAsync(string key, string? valueJson, ConfigurationContext context, CancellationToken ct = default)
    {
        var definition = await RequireDefinitionAsync(key, ct);
        if (!definition.TenantConfigurable || !definition.TenantCanOverride)
            throw new InvalidOperationException($"Configuration key '{key}' cannot be overridden at tenant scope.");
        if (context.TenantId is not { } tenantId)
            throw new InvalidOperationException("Tenant scope requires a TenantId.");

        ValidateValue(definition, valueJson);
        var old = await repository.GetTenantValueAsync(tenantId, key, ct);
        await repository.UpsertTenantValueAsync(tenantId, key, valueJson, context.UserId, ct);
        await RecordChangeAsync(definition, tenantId, matterId: null, "Tenant", old?.ValueJson, valueJson, context.UserId, ct);
    }

    public async Task SetPlatformValueAsync(string key, string? valueJson, ConfigurationContext context, CancellationToken ct = default)
    {
        var definition = await RequireDefinitionAsync(key, ct);
        ValidateValue(definition, valueJson);
        var old = await repository.GetPlatformValueAsync(key, ct);
        await repository.UpsertPlatformValueAsync(key, valueJson, context.UserId, ct);
        await RecordChangeAsync(definition, tenantId: null, matterId: null, "Platform", old?.ValueJson, valueJson, context.UserId, ct);
    }

    public async Task SetMatterValueAsync(string key, string? valueJson, ConfigurationContext context, CancellationToken ct = default)
    {
        var definition = await RequireDefinitionAsync(key, ct);
        if (!definition.MatterConfigurable || !definition.TenantCanOverride)
            throw new InvalidOperationException($"Configuration key '{key}' cannot be overridden at matter scope.");
        if (context.TenantId is not { } tenantId || context.MatterId is not { } matterId)
            throw new InvalidOperationException("Matter scope requires a TenantId and MatterId.");

        ValidateValue(definition, valueJson);
        var old = await repository.GetMatterValueAsync(tenantId, matterId, key, ct);
        await repository.UpsertMatterValueAsync(tenantId, matterId, key, valueJson, context.UserId, ct);
        await RecordChangeAsync(definition, tenantId, matterId, "Matter", old?.ValueJson, valueJson, context.UserId, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private async Task<ConfigurationDefinition> RequireDefinitionAsync(string key, CancellationToken ct)
        => await repository.GetDefinitionAsync(key, ct)
            ?? throw new InvalidOperationException($"Configuration key '{key}' is not defined.");

    private async Task RecordChangeAsync(
        ConfigurationDefinition definition, Guid? tenantId, Guid? matterId, string scope,
        string? oldJson, string? newJson, Guid? actorUserId, CancellationToken ct)
    {
        var sensitive = definition.Sensitivity is ConfigurationSensitivity.Sensitive or ConfigurationSensitivity.Secret;
        var oldValue = sensitive ? Redacted : oldJson;
        var newValue = sensitive ? Redacted : newJson;

        var history = new ConfigurationHistoryRow(
            Guid.NewGuid(), tenantId, matterId, definition.Key, scope, oldValue, newValue, actorUserId, DateTime.UtcNow);
        await repository.AppendChangeHistoryAsync(history, correlationId: null, ct);

        var dataJson = JsonSerializer.Serialize(new { key = definition.Key, scope, changed = true });
        await saasRepository.WriteAuditAsync(tenantId, actorUserId, "CONFIGURATION_CHANGED", null, "Configuration", null, dataJson, null, ct);
    }

    private static void ValidateValue(ConfigurationDefinition definition, string? valueJson)
    {
        if (valueJson is null)
            return;

        var validation = definition.Validation;
        switch (definition.ValueType)
        {
            case ConfigurationValueType.Integer:
            case ConfigurationValueType.Decimal:
                var number = ParseNumber(valueJson);
                if (validation?.Min is { } min && number < min)
                    throw new InvalidOperationException($"'{definition.Key}' must be >= {min}.");
                if (validation?.Max is { } max && number > max)
                    throw new InvalidOperationException($"'{definition.Key}' must be <= {max}.");
                break;

            case ConfigurationValueType.Enum:
                if (validation?.Options is { Count: > 0 } options)
                {
                    var value = ParseString(valueJson);
                    if (!options.Contains(value, StringComparer.Ordinal))
                        throw new InvalidOperationException($"'{definition.Key}' must be one of: {string.Join(", ", options)}.");
                }
                break;

            case ConfigurationValueType.Boolean:
                _ = JsonSerializer.Deserialize<bool>(valueJson);
                break;
        }
    }

    private static decimal ParseNumber(string json)
    {
        try { return JsonSerializer.Deserialize<decimal>(json); }
        catch (JsonException) { throw new InvalidOperationException("Value is not a valid number."); }
    }

    private static string ParseString(string json)
    {
        try { return JsonSerializer.Deserialize<string>(json) ?? string.Empty; }
        catch (JsonException) { return json; }
    }

    private static ResolvedConfiguration<T> Resolved<T>(string json, ConfigurationScope scope, bool overridden, bool canOverride)
    {
        var value = typeof(T) == typeof(string) && !json.TrimStart().StartsWith('"')
            ? (T)(object)json
            : JsonSerializer.Deserialize<T>(json)!;
        return new ResolvedConfiguration<T>(value, scope, overridden, canOverride);
    }
}
