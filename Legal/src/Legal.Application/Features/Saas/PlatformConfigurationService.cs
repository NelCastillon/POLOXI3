using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Platform configuration read model (/platform/configuration).
//
// Assembles the full DB-backed registry into UI items with the effective platform
// value and its provenance (Platform value → Code default), and delegates writes
// to IConfigurationResolver so validation, change history, and audit all run.
// Super Admin only; editing a value here changes the default every tenant inherits
// unless the tenant overrides it. DB stays the source of truth.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class PlatformConfigurationService(
    IConfigurationRepository repository,
    IConfigurationResolver resolver) : IPlatformConfigurationService
{
    public async Task<IReadOnlyList<PlatformConfigurationCategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var definitions = await repository.GetDefinitionsAsync(ct);
        var items = new List<PlatformConfigurationItemDto>();

        foreach (var definition in definitions)
        {
            var platformValue = await repository.GetPlatformValueAsync(definition.Key, ct);

            string? effective;
            string source;
            bool overridden;

            if (platformValue?.ValueJson is { } pjson)
            {
                effective = pjson;
                source = nameof(ConfigurationScope.Platform);
                overridden = true;
            }
            else
            {
                effective = definition.DefaultValueJson;
                source = nameof(ConfigurationScope.CodeDefault);
                overridden = false;
            }

            items.Add(new PlatformConfigurationItemDto
            {
                Key = definition.Key,
                Category = definition.Category,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                ValueType = definition.ValueType.ToString(),
                DefaultValueJson = definition.DefaultValueJson,
                EffectiveValueJson = effective,
                PlatformValueJson = platformValue?.ValueJson,
                Source = source,
                IsOverridden = overridden,
                TenantCanOverride = definition.TenantCanOverride,
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
            .Select(g => new PlatformConfigurationCategoryDto(
                g.Key,
                g.OrderBy(i => i.SortOrder).ThenBy(i => i.DisplayName, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    public Task SetPlatformValueAsync(Guid? actorUserId, string key, string? valueJson, CancellationToken ct = default)
    {
        var context = new ConfigurationContext(TenantId: null, MatterId: null, UserId: actorUserId, Environment: null);
        return resolver.SetPlatformValueAsync(key, valueJson, context, ct);
    }
}
