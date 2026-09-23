using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Saas;
using System.Text.Json;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Configuration persistence (migration 0248).
//
// Dapper access to SaaS.Config_Definition and the scoped value tables, plus the
// append-only SaaS.Config_ChangeHistory. Upserts are idempotent (soft-delete
// aware) and honour the same conventions as SaasRepository.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ConfigurationRepository(ISqlConnectionFactory connectionFactory) : IConfigurationRepository
{
    // ── Definitions ──────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<ConfigurationDefinition>> GetDefinitionsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ConfigKey, Category, DisplayName, Description, ValueType, DefaultValueJson, Scope,
                   TenantConfigurable, MatterConfigurable, ExecutionConfigurable, TenantCanOverride,
                   Sensitivity, RequiresRestart, RequiredPermission, ValidationJson, SortOrder
            FROM SaaS.Config_Definition
            WHERE IsDeleted = 0
            ORDER BY Category, SortOrder, DisplayName;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<DefinitionRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<ConfigurationDefinition?> GetDefinitionAsync(string key, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ConfigKey, Category, DisplayName, Description, ValueType, DefaultValueJson, Scope,
                   TenantConfigurable, MatterConfigurable, ExecutionConfigurable, TenantCanOverride,
                   Sensitivity, RequiresRestart, RequiredPermission, ValidationJson, SortOrder
            FROM SaaS.Config_Definition
            WHERE ConfigKey = @Key AND IsDeleted = 0;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<DefinitionRow>(new CommandDefinition(sql, new { Key = key }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    // ── Scoped value reads ───────────────────────────────────────────────────
    public async Task<ConfigurationValueRow?> GetPlatformValueAsync(string key, CancellationToken ct = default)
    {
        const string sql = "SELECT ConfigKey, ValueJson FROM SaaS.Config_PlatformValue WHERE ConfigKey = @Key AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<ConfigurationValueRow>(new CommandDefinition(sql, new { Key = key }, cancellationToken: ct));
    }

    public async Task<ConfigurationValueRow?> GetTenantValueAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        const string sql = "SELECT ConfigKey, ValueJson FROM SaaS.Config_TenantValue WHERE TenantId = @TenantId AND ConfigKey = @Key AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<ConfigurationValueRow>(new CommandDefinition(sql, new { TenantId = tenantId, Key = key }, cancellationToken: ct));
    }

    public async Task<ConfigurationValueRow?> GetMatterValueAsync(Guid tenantId, Guid matterId, string key, CancellationToken ct = default)
    {
        const string sql = "SELECT ConfigKey, ValueJson FROM SaaS.Config_MatterValue WHERE TenantId = @TenantId AND MatterId = @MatterId AND ConfigKey = @Key AND IsDeleted = 0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<ConfigurationValueRow>(new CommandDefinition(sql, new { TenantId = tenantId, MatterId = matterId, Key = key }, cancellationToken: ct));
    }

    // ── Scoped value writes (upsert) ─────────────────────────────────────────
    public async Task UpsertPlatformValueAsync(string key, string? valueJson, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.Config_PlatformValue
                SET ValueJson = @ValueJson, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
                WHERE ConfigKey = @Key AND IsDeleted = 0;
            IF @@ROWCOUNT = 0
                INSERT SaaS.Config_PlatformValue (ConfigKey, ValueJson, CreatedByUserId)
                VALUES (@Key, @ValueJson, @ActorUserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Key = key, ValueJson = valueJson, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task UpsertTenantValueAsync(Guid tenantId, string key, string? valueJson, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.Config_TenantValue
                SET ValueJson = @ValueJson, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
                WHERE TenantId = @TenantId AND ConfigKey = @Key AND IsDeleted = 0;
            IF @@ROWCOUNT = 0
                INSERT SaaS.Config_TenantValue (TenantId, ConfigKey, ValueJson, CreatedByUserId)
                VALUES (@TenantId, @Key, @ValueJson, @ActorUserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Key = key, ValueJson = valueJson, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    public async Task UpsertMatterValueAsync(Guid tenantId, Guid matterId, string key, string? valueJson, Guid? actorUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE SaaS.Config_MatterValue
                SET ValueJson = @ValueJson, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND ConfigKey = @Key AND IsDeleted = 0;
            IF @@ROWCOUNT = 0
                INSERT SaaS.Config_MatterValue (TenantId, MatterId, ConfigKey, ValueJson, CreatedByUserId)
                VALUES (@TenantId, @MatterId, @Key, @ValueJson, @ActorUserId);
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, MatterId = matterId, Key = key, ValueJson = valueJson, ActorUserId = actorUserId }, cancellationToken: ct));
    }

    // ── History ──────────────────────────────────────────────────────────────
    public async Task AppendChangeHistoryAsync(ConfigurationHistoryRow row, string? correlationId, CancellationToken ct = default)
    {
        const string sql = """
            INSERT SaaS.Config_ChangeHistory
                (TenantId, MatterId, ConfigKey, Scope, OldValueJson, NewValueJson, ActorUserId, CorrelationId, OccurredAtUtc)
            VALUES
                (@TenantId, @MatterId, @ConfigKey, @Scope, @OldValueJson, @NewValueJson, @ActorUserId, @CorrelationId, SYSUTCDATETIME());
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            row.TenantId,
            row.MatterId,
            row.ConfigKey,
            row.Scope,
            row.OldValueJson,
            row.NewValueJson,
            row.ActorUserId,
            CorrelationId = correlationId
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ConfigurationHistoryRow>> GetChangeHistoryAsync(Guid? tenantId, string? key, int take, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@Take) ConfigChangeHistoryId, TenantId, MatterId, ConfigKey, Scope,
                   OldValueJson, NewValueJson, ActorUserId, OccurredAtUtc
            FROM SaaS.Config_ChangeHistory
            WHERE IsDeleted = 0
              AND (@TenantId IS NULL OR TenantId = @TenantId)
              AND (@Key IS NULL OR ConfigKey = @Key)
            ORDER BY OccurredAtUtc DESC;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<ConfigurationHistoryRow>(new CommandDefinition(sql, new { TenantId = tenantId, Key = key, Take = take <= 0 ? 100 : take }, cancellationToken: ct));
        return rows.ToList();
    }

    // ── Mapping ──────────────────────────────────────────────────────────────
    private sealed record DefinitionRow(
        string ConfigKey, string Category, string DisplayName, string? Description,
        string ValueType, string DefaultValueJson, string Scope,
        bool TenantConfigurable, bool MatterConfigurable, bool ExecutionConfigurable, bool TenantCanOverride,
        string Sensitivity, bool RequiresRestart, string? RequiredPermission, string? ValidationJson, int SortOrder);

    private static ConfigurationDefinition Map(DefinitionRow r) => new()
    {
        Key = r.ConfigKey,
        Category = r.Category,
        DisplayName = r.DisplayName,
        Description = r.Description,
        ValueType = Enum.Parse<ConfigurationValueType>(r.ValueType, ignoreCase: true),
        DefaultValueJson = r.DefaultValueJson,
        OwnerScope = Enum.TryParse<ConfigurationScope>(r.Scope, ignoreCase: true, out var scope) ? scope : ConfigurationScope.Platform,
        TenantConfigurable = r.TenantConfigurable,
        MatterConfigurable = r.MatterConfigurable,
        ExecutionConfigurable = r.ExecutionConfigurable,
        TenantCanOverride = r.TenantCanOverride,
        Sensitivity = Enum.TryParse<ConfigurationSensitivity>(r.Sensitivity, ignoreCase: true, out var sens) ? sens : ConfigurationSensitivity.Public,
        RequiresRestart = r.RequiresRestart,
        RequiredPermission = r.RequiredPermission,
        Validation = ParseValidation(r.ValidationJson),
        SortOrder = r.SortOrder
    };

    private static ConfigurationValidation? ParseValidation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<ConfigurationValidation>(json, ValidationOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions ValidationOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
