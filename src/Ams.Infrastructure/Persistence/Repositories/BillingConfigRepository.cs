using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.BillingConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BillingConfigRepository : IBillingConfigRepository
{
    private readonly ISqlConnectionFactory _cf;
    public BillingConfigRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = "BillingConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc";

    public async Task<BillingConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<BillingConfigItemDto>(new CommandDefinition($"SELECT {Cols} FROM Billing.BillingConfigItem WHERE BillingConfigItemId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<BillingConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
SELECT BillingConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc
FROM Billing.BillingConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%')
ORDER BY SortOrder ASC, Name ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Billing.BillingConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        return new() { Items = (await multi.ReadAsync<BillingConfigItemDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateBillingConfigItemRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Billing.BillingConfigItem (BillingConfigItemId,TenantId,Kind,Code,Name,Category,Description,ConfigurationJson,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@Kind,@Code,@Name,@Category,@Description,@ConfigurationJson,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.Kind, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateBillingConfigItemRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Billing.BillingConfigItem SET Code=@Code,Name=@Name,Category=@Category,Description=@Description,ConfigurationJson=@ConfigurationJson,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE BillingConfigItemId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Billing.BillingConfigItem SET IsDeleted=1 WHERE BillingConfigItemId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
