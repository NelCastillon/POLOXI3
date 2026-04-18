using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TrialBalanceSnapshotRepository : ITrialBalanceSnapshotRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.TrialBalanceSnapshot",
        "TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "AccountCode LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%'",
        "AccountCode ASC");

    public TrialBalanceSnapshotRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<TrialBalanceSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.TrialBalanceSnapshot WHERE TrialBalanceSnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TrialBalanceSnapshotDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TrialBalanceSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TrialBalanceSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TrialBalanceSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
