using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PeriodCloseEntryRepository : IPeriodCloseEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.PeriodCloseEntry",
        "PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "TaskDescription LIKE '%' + @SearchTerm + '%'",
        "CreatedDateUtc DESC");

    public PeriodCloseEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PeriodCloseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.PeriodCloseEntry WHERE PeriodCloseEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PeriodCloseEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PeriodCloseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PeriodCloseEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PeriodCloseEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
