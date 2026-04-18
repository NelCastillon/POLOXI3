using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.AccountingPeriod",
        "AccountingPeriodId, TenantId, PeriodName, FiscalYear, PeriodNumber, StartDate, EndDate, StatusCode, ClosedDateUtc, ClosedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "PeriodName LIKE '%' + @SearchTerm + '%'",
        "FiscalYear DESC, PeriodNumber ASC");

    public AccountingPeriodRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AccountingPeriodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT AccountingPeriodId, TenantId, PeriodName, FiscalYear, PeriodNumber, StartDate, EndDate, StatusCode, ClosedDateUtc, ClosedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.AccountingPeriod WHERE AccountingPeriodId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountingPeriodDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountingPeriodDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AccountingPeriodDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccountingPeriodDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
