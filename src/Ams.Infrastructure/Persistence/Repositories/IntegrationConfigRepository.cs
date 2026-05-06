using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.IntegrationConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class IntegrationConfigRepository : IIntegrationConfigRepository
{
    private readonly ISqlConnectionFactory _cf;
    public IntegrationConfigRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = "IntegrationConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc";

    public async Task<IntegrationConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<IntegrationConfigItemDto>(new CommandDefinition($"SELECT {Cols} FROM Integration.IntegrationConfigItem WHERE IntegrationConfigItemId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<IntegrationConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
SELECT IntegrationConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc
FROM Integration.IntegrationConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%')
ORDER BY SortOrder ASC, Name ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Integration.IntegrationConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        return new() { Items = (await multi.ReadAsync<IntegrationConfigItemDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateIntegrationConfigItemRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Integration.IntegrationConfigItem (IntegrationConfigItemId,TenantId,Kind,Code,Name,Category,Description,ConfigurationJson,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@Kind,@Code,@Name,@Category,@Description,@ConfigurationJson,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.Kind, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateIntegrationConfigItemRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Integration.IntegrationConfigItem SET Code=@Code,Name=@Name,Category=@Category,Description=@Description,ConfigurationJson=@ConfigurationJson,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE IntegrationConfigItemId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Integration.IntegrationConfigItem SET IsDeleted=1 WHERE IntegrationConfigItemId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
