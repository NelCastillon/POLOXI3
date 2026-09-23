using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Tenant configuration read model (/admin/configuration).
//
// Assembles the DB-backed registry into UI items with the effective value and its
// provenance for a specific tenant, and delegates writes to IConfigurationResolver
// so validation, change history, and audit all run. DB stays the source of truth.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class TenantConfigurationService(
    IConfigurationRepository repository,
    IConfigurationResolver resolver) : ITenantConfigurationService
{
    public async Task<IReadOnlyList<TenantConfigurationCategoryDto>> GetForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var definitions = await repository.GetDefinitionsAsync(ct);
        var items = new List<TenantConfigurationItemDto>();

        foreach (var definition in definitions.Where(d => d.TenantConfigurable))
        {
            var tenantValue = definition.TenantCanOverride
                ? await repository.GetTenantValueAsync(tenantId, definition.Key, ct)
                : null;
            var platformValue = await repository.GetPlatformValueAsync(definition.Key, ct);

            string? effective;
            string source;
            bool overridden;

            if (tenantValue?.ValueJson is { } tjson)
            {
                effective = tjson;
                source = nameof(ConfigurationScope.Tenant);
                overridden = true;
            }
            else if (platformValue?.ValueJson is { } pjson)
            {
                effective = pjson;
                source = nameof(ConfigurationScope.Platform);
                overridden = false;
            }
            else
            {
                effective = definition.DefaultValueJson;
                source = nameof(ConfigurationScope.CodeDefault);
                overridden = false;
            }

            items.Add(new TenantConfigurationItemDto
            {
                Key = definition.Key,
                Category = definition.Category,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                ValueType = definition.ValueType.ToString(),
                DefaultValueJson = definition.DefaultValueJson,
                EffectiveValueJson = effective,
                TenantValueJson = tenantValue?.ValueJson,
                Source = source,
                IsOverridden = overridden,
                CanOverride = definition.TenantCanOverride,
                Sensitive = definition.Sensitivity is ConfigurationSensitivity.Sensitive or ConfigurationSensitivity.Secret,
                RequiredPermission = definition.RequiredPermission,
                Options = definition.Validation?.Options,
                Min = definition.Validation?.Min,
                Max = definition.Validation?.Max,
                SortOrder = definition.SortOrder
            });
        }

        return items
            .GroupBy(i => i.Category)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TenantConfigurationCategoryDto(
                g.Key,
                g.OrderBy(i => i.SortOrder).ThenBy(i => i.DisplayName, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    public Task SetTenantValueAsync(Guid tenantId, Guid? actorUserId, string key, string? valueJson, CancellationToken ct = default)
    {
        var context = new ConfigurationContext(tenantId, MatterId: null, UserId: actorUserId, Environment: null);
        return resolver.SetTenantValueAsync(key, valueJson, context, ct);
    }
}
