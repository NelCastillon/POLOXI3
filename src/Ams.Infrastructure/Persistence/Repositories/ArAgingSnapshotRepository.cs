using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ArAgingSnapshotRepository : IArAgingSnapshotRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ArAgingSnapshotRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ArAgingSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc FROM Billing.ArAgingSnapshot WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ArAgingSnapshotDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ArAgingSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.ArAgingSnapshot", "SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc", "CAST(AccountId AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'", "SnapshotDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ArAgingSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ArAgingSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
