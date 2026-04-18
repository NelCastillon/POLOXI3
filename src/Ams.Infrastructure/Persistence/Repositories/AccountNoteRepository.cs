using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountNotes;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountNoteRepository : IAccountNoteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountNoteRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Guid> CreateAsync(CreateAccountNoteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Client.AccountNote
    (AccountNoteId, TenantId, AccountId, NoteText, NoteTypeCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@AccountNoteId, @TenantId, @AccountId, @NoteText, @NoteTypeCode, SYSUTCDATETIME(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AccountNoteId = id,
            request.TenantId,
            request.AccountId,
            request.NoteText,
            request.NoteTypeCode,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<AccountNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT n.AccountNoteId, n.TenantId, n.AccountId, ISNULL(a.AccountName, '') AS AccountName,
       n.NoteText, n.NoteTypeCode, n.CreatedByUserId, n.CreatedDateUtc
FROM Client.AccountNote n
LEFT JOIN Client.Account a ON a.AccountId = n.AccountId
WHERE n.AccountNoteId = @Id AND n.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountNoteDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountNoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT n.AccountNoteId, n.TenantId, n.AccountId, ISNULL(a.AccountName, '') AS AccountName,
           n.NoteText, n.NoteTypeCode, n.CreatedByUserId, n.CreatedDateUtc
    FROM Client.AccountNote n
    LEFT JOIN Client.Account a ON a.AccountId = n.AccountId
    WHERE n.TenantId = @TenantId AND n.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR n.NoteText LIKE '%' + @SearchTerm + '%'
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.AccountNote
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR NoteText LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AccountNoteDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AccountNoteDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
