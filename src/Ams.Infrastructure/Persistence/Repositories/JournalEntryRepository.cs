using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public JournalEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<JournalEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT JournalEntryId, TenantId, JournalNumber, EntryDate, Description, StatusCode, PostedDateUtc, PostedByUserId, CreatedDateUtc FROM Finance.JournalEntry WHERE JournalEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<JournalEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<JournalEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Finance.JournalEntry", "JournalEntryId, TenantId, JournalNumber, EntryDate, Description, StatusCode, PostedDateUtc, PostedByUserId, CreatedDateUtc", "JournalNumber LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%'", "EntryDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<JournalEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<JournalEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
