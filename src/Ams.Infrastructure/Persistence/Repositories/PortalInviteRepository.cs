using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PortalInvites;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PortalInviteRepository : IPortalInviteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PortalInviteRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Guid> CreateAsync(CreatePortalInviteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Client.PortalInvite
(
    PortalInviteId, TenantId, ContactId, AccountId, InviteToken,
    InviteEmail, StatusCode, ExpiresDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @PortalInviteId, @TenantId, @ContactId, @AccountId, @InviteToken,
    @InviteEmail, 'Pending', @ExpiresDateUtc, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PortalInviteId = id,
            request.TenantId,
            request.ContactId,
            request.AccountId,
            InviteToken = Guid.NewGuid().ToString("N"),
            request.InviteEmail,
            request.ExpiresDateUtc,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<PortalInviteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.PortalInviteId, p.TenantId, p.ContactId,
       ISNULL(c.FirstName + ' ' + c.LastName, '') AS ContactName,
       p.AccountId, ISNULL(a.AccountName, '') AS AccountName,
       p.InviteEmail, p.StatusCode, p.SentDateUtc, p.ExpiresDateUtc,
       p.AcceptedDateUtc, p.CreatedByUserId, p.CreatedDateUtc
FROM Client.PortalInvite p
LEFT JOIN Client.Contact c ON c.ContactId = p.ContactId
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
WHERE p.PortalInviteId = @Id AND p.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PortalInviteDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PortalInviteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT p.PortalInviteId, p.TenantId, p.ContactId,
           ISNULL(c.FirstName + ' ' + c.LastName, '') AS ContactName,
           p.AccountId, ISNULL(a.AccountName, '') AS AccountName,
           p.InviteEmail, p.StatusCode, p.SentDateUtc, p.ExpiresDateUtc,
           p.AcceptedDateUtc, p.CreatedByUserId, p.CreatedDateUtc
    FROM Client.PortalInvite p
    LEFT JOIN Client.Contact c ON c.ContactId = p.ContactId
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR p.InviteEmail LIKE '%' + @SearchTerm + '%'
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
           OR c.FirstName LIKE '%' + @SearchTerm + '%'
           OR c.LastName LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.PortalInvite p
LEFT JOIN Client.Contact c ON c.ContactId = p.ContactId
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR p.InviteEmail LIKE '%' + @SearchTerm + '%'
       OR a.AccountName LIKE '%' + @SearchTerm + '%'
       OR c.FirstName LIKE '%' + @SearchTerm + '%'
       OR c.LastName LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<PortalInviteDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<PortalInviteDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
