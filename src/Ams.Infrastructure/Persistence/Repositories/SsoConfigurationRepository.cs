using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SsoConfigurationRepository : ISsoConfigurationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SsoConfigurationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<SsoConfigurationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT SsoConfigId, TenantId, ProviderTypeCode, ProviderName, MetadataUrl, ClientId, TenantDomain, IsEnabled, RequireSso, AllowLocalLogin, CreatedDateUtc, ModifiedDateUtc FROM IAM.SsoConfiguration WHERE SsoConfigId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SsoConfigurationDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<SsoConfigurationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("IAM.SsoConfiguration", "SsoConfigId, TenantId, ProviderTypeCode, ProviderName, MetadataUrl, ClientId, TenantDomain, IsEnabled, RequireSso, AllowLocalLogin, CreatedDateUtc, ModifiedDateUtc", "ProviderName LIKE '%' + @SearchTerm + '%' OR ProviderTypeCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SsoConfigurationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SsoConfigurationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
