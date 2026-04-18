using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public TenantRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TenantId, TenantCode, TenantName, PlanCode, IsActive, Locale, CurrencyCode, TimeZoneId, CreatedDateUtc FROM Core.Tenant WHERE TenantId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TenantDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TenantDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT TenantId, TenantCode, TenantName, PlanCode, IsActive, Locale, CurrencyCode, TimeZoneId, CreatedDateUtc FROM Core.Tenant WHERE IsDeleted = 0 AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TenantName LIKE '%' + @SearchTerm + '%' OR TenantCode LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Core.Tenant WHERE IsDeleted = 0 AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TenantName LIKE '%' + @SearchTerm + '%' OR TenantCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TenantDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TenantDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
