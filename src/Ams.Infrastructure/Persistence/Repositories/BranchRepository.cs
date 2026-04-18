using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public BranchRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<BranchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT BranchId, TenantId, BranchCode, BranchName, City, StateProvince, CountryCode, IsActive, CreatedDateUtc FROM Core.Branch WHERE BranchId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BranchDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BranchDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Core.Branch", "BranchId, TenantId, BranchCode, BranchName, City, StateProvince, CountryCode, IsActive, CreatedDateUtc", "BranchName LIKE '%' + @SearchTerm + '%' OR BranchCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BranchDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BranchDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
