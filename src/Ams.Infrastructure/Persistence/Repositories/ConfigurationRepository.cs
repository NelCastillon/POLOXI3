using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ConfigurationRepository : IConfigurationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ConfigurationRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<ConfigurationSettingDto?> GetByIdAsync(Guid settingId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SettingId, TenantId, ScopeCode, ScopeEntityId, SettingKey, SettingValue,
                   DataTypeCode, DefaultValue, Description, IsEncrypted, IsReadOnly,
                   ModuleCode, CreatedDateUtc, ModifiedDateUtc
            FROM Core.ConfigurationSetting
            WHERE SettingId = @SettingId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ConfigurationSettingDto>(
            new CommandDefinition(sql, new { SettingId = settingId }, cancellationToken: cancellationToken));
    }

    public async Task<ConfigurationSettingDto?> GetByKeyAsync(string settingKey, string scopeCode, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SettingId, TenantId, ScopeCode, ScopeEntityId, SettingKey, SettingValue,
                   DataTypeCode, DefaultValue, Description, IsEncrypted, IsReadOnly,
                   ModuleCode, CreatedDateUtc, ModifiedDateUtc
            FROM Core.ConfigurationSetting
            WHERE SettingKey = @SettingKey AND ScopeCode = @ScopeCode
              AND (@TenantId IS NULL OR TenantId = @TenantId OR TenantId IS NULL)
              AND IsDeleted = 0
            ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryFirstOrDefaultAsync<ConfigurationSettingDto>(
            new CommandDefinition(sql, new { SettingKey = settingKey, ScopeCode = scopeCode, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ConfigurationSettingDto>> SearchAsync(string? searchTerm, string? scopeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT SettingId, TenantId, ScopeCode, ScopeEntityId, SettingKey, SettingValue,
                       DataTypeCode, DefaultValue, Description, IsEncrypted, IsReadOnly,
                       ModuleCode, CreatedDateUtc, ModifiedDateUtc
                FROM Core.ConfigurationSetting
                WHERE IsDeleted = 0
                  AND (@ScopeCode IS NULL OR ScopeCode = @ScopeCode)
                  AND (@SearchTerm IS NULL OR SettingKey   LIKE '%' + @SearchTerm + '%'
                                          OR Description   LIKE '%' + @SearchTerm + '%'
                                          OR ModuleCode    = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY ScopeCode ASC, SettingKey ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.ConfigurationSetting
            WHERE IsDeleted = 0
              AND (@ScopeCode IS NULL OR ScopeCode = @ScopeCode)
              AND (@SearchTerm IS NULL OR SettingKey LIKE '%' + @SearchTerm + '%'
                                      OR Description LIKE '%' + @SearchTerm + '%'
                                      OR ModuleCode  = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { SearchTerm = searchTerm, ScopeCode = scopeCode, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ConfigurationSettingDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ConfigurationSettingDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
