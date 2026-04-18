using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountOwnerHistoryRepository : IAccountOwnerHistoryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountOwnerHistoryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AccountOwnerHistoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT h.HistoryId, h.TenantId, h.AccountId, ISNULL(a.AccountName, '') AS AccountName,
       h.PreviousOwnerUserId, ISNULL(u1.FullName, '') AS PreviousOwnerName,
       h.NewOwnerUserId, ISNULL(u2.FullName, '') AS NewOwnerName,
       h.ChangedDateUtc, h.ChangedByUserId, h.Notes
FROM Client.AccountOwnerHistory h
LEFT JOIN Client.Account a ON a.AccountId = h.AccountId
LEFT JOIN IAM.[User] u1 ON u1.UserId = h.PreviousOwnerUserId
LEFT JOIN IAM.[User] u2 ON u2.UserId = h.NewOwnerUserId
WHERE h.HistoryId = @Id AND h.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountOwnerHistoryDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountOwnerHistoryDto>> SearchAsync(Guid tenantId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT h.HistoryId, h.TenantId, h.AccountId, ISNULL(a.AccountName, '') AS AccountName,
           h.PreviousOwnerUserId, ISNULL(u1.FullName, '') AS PreviousOwnerName,
           h.NewOwnerUserId, ISNULL(u2.FullName, '') AS NewOwnerName,
           h.ChangedDateUtc, h.ChangedByUserId, h.Notes
    FROM Client.AccountOwnerHistory h
    LEFT JOIN Client.Account a ON a.AccountId = h.AccountId
    LEFT JOIN IAM.[User] u1 ON u1.UserId = h.PreviousOwnerUserId
    LEFT JOIN IAM.[User] u2 ON u2.UserId = h.NewOwnerUserId
    WHERE h.TenantId = @TenantId AND h.IsDeleted = 0
      AND (@AccountId IS NULL OR h.AccountId = @AccountId)
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
           OR ISNULL(u1.FullName, '') LIKE '%' + @SearchTerm + '%'
           OR ISNULL(u2.FullName, '') LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY ChangedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.AccountOwnerHistory h
LEFT JOIN Client.Account a ON a.AccountId = h.AccountId
LEFT JOIN IAM.[User] u1 ON u1.UserId = h.PreviousOwnerUserId
LEFT JOIN IAM.[User] u2 ON u2.UserId = h.NewOwnerUserId
WHERE h.TenantId = @TenantId AND h.IsDeleted = 0
  AND (@AccountId IS NULL OR h.AccountId = @AccountId)
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR a.AccountName LIKE '%' + @SearchTerm + '%'
       OR ISNULL(u1.FullName, '') LIKE '%' + @SearchTerm + '%'
       OR ISNULL(u2.FullName, '') LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            AccountId = accountId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AccountOwnerHistoryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AccountOwnerHistoryDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
