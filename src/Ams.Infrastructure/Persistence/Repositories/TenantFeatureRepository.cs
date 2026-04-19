using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantFeatures;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantFeatureRepository : ITenantFeatureRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TenantFeatureRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<TenantFeatureDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Returns all catalog features, LEFT JOINed with tenant overrides so every
        // feature appears in the grid — tenant overrides override the plan default.
        const string sql = @"
SELECT
    ISNULL(tf.TenantFeatureId, NEWID())     AS TenantFeatureId,
    @TenantId                               AS TenantId,
    f.FeatureCode,
    f.FeatureName,
    f.Module,
    ISNULL(tf.IsEnabled, f.DefaultEnabled)  AS IsEnabled,
    tf.EffectiveStartUtc,
    tf.EffectiveEndUtc,
    ISNULL(tf.SourceType, 'PlanDefault')    AS SourceType,
    ISNULL(tf.EnabledDateUtc, f.CreatedDateUtc) AS EnabledDateUtc,
    tf.ModifiedDateUtc
FROM Core.Feature f
LEFT JOIN Core.TenantFeature tf
    ON tf.FeatureCode = f.FeatureCode
    AND tf.TenantId   = @TenantId
WHERE f.IsEnabled = 1
ORDER BY f.Module, f.FeatureCode;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<TenantFeatureDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task UpsertOverrideAsync(Guid tenantId, OverrideTenantFeatureRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
MERGE Core.TenantFeature AS target
USING (SELECT @TenantId AS TenantId, @FeatureCode AS FeatureCode) AS src
    ON target.TenantId = src.TenantId AND target.FeatureCode = src.FeatureCode
WHEN MATCHED THEN
    UPDATE SET
        IsEnabled         = @IsEnabled,
        EffectiveStartUtc = @EffectiveStartUtc,
        EffectiveEndUtc   = @EffectiveEndUtc,
        SourceType        = @SourceType,
        ModifiedDateUtc   = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (TenantFeatureId, TenantId, FeatureCode, IsEnabled, EffectiveStartUtc, EffectiveEndUtc, SourceType, EnabledDateUtc)
    VALUES (NEWID(), @TenantId, @FeatureCode, @IsEnabled, @EffectiveStartUtc, @EffectiveEndUtc, @SourceType, SYSUTCDATETIME());";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId          = tenantId,
            FeatureCode       = request.FeatureCode,
            IsEnabled         = request.IsEnabled,
            EffectiveStartUtc = request.EffectiveStartUtc,
            EffectiveEndUtc   = request.EffectiveEndUtc,
            SourceType        = request.SourceType
        }, cancellationToken: cancellationToken));
    }

    public async Task SetEnabledAsync(Guid tenantId, string featureCode, bool enabled, CancellationToken cancellationToken = default)
    {
        const string sql = @"
MERGE Core.TenantFeature AS target
USING (SELECT @TenantId AS TenantId, @FeatureCode AS FeatureCode) AS src
    ON target.TenantId = src.TenantId AND target.FeatureCode = src.FeatureCode
WHEN MATCHED THEN
    UPDATE SET IsEnabled = @Enabled, SourceType = 'Manual', ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (TenantFeatureId, TenantId, FeatureCode, IsEnabled, SourceType, EnabledDateUtc)
    VALUES (NEWID(), @TenantId, @FeatureCode, @Enabled, 'Manual', SYSUTCDATETIME());";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, FeatureCode = featureCode, Enabled = enabled },
            cancellationToken: cancellationToken));
    }

    public async Task ResetToDefaultAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Core.TenantFeature WHERE TenantId = @TenantId AND FeatureCode = @FeatureCode;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, FeatureCode = featureCode },
            cancellationToken: cancellationToken));
    }
}
