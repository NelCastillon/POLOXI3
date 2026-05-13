using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
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
        if (!await TableExistsAsync(cancellationToken)) return null;

        const string sql = "SELECT PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.PeriodCloseEntry WHERE PeriodCloseEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PeriodCloseEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PeriodCloseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            return new PagedResult<PeriodCloseEntryDto> { Items = [], TotalCount = 0, PageNumber = pageNumber, PageSize = pageSize };

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PeriodCloseEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PeriodCloseEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.PeriodCloseEntry does not exist in the current database schema. Period Close is unavailable until the database schema includes this table.");

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.PeriodCloseEntry (PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountingPeriodId, @TaskDescription, @StatusCode, @CompletedByUserId, @CompletedDateUtc, @Notes, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountingPeriodId, request.TaskDescription, request.StatusCode, request.CompletedByUserId, request.CompletedDateUtc, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.PeriodCloseEntry does not exist in the current database schema. Period Close is unavailable until the database schema includes this table.");

        const string sql = @"
UPDATE Finance.PeriodCloseEntry
SET AccountingPeriodId = @AccountingPeriodId,
    TaskDescription = @TaskDescription,
    StatusCode = @StatusCode,
    CompletedByUserId = @CompletedByUserId,
    CompletedDateUtc = @CompletedDateUtc,
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE PeriodCloseEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountingPeriodId, request.TaskDescription, request.StatusCode, request.CompletedByUserId, request.CompletedDateUtc, request.Notes, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task<bool> TableExistsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'Finance.PeriodCloseEntry', N'U') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
