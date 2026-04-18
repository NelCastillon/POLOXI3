using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CollectionsNoteRepository : ICollectionsNoteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CollectionsNoteRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CollectionsNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CollectionsNoteId, TenantId, AccountId, InvoiceId, NoteDate, NoteText, ActionCode, NextFollowUpDate, CreatedByUserId, CreatedDateUtc FROM Billing.CollectionsNote WHERE CollectionsNoteId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CollectionsNoteDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CollectionsNoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.CollectionsNote", "CollectionsNoteId, TenantId, AccountId, InvoiceId, NoteDate, NoteText, ActionCode, NextFollowUpDate, CreatedByUserId, CreatedDateUtc", "NoteText LIKE '%' + @SearchTerm + '%' OR ActionCode LIKE '%' + @SearchTerm + '%'", "NoteDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CollectionsNoteDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CollectionsNoteDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
