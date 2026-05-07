using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Contacts;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ContactRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Guid> CreateAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Client.Contact
(
    ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone, JobTitle,
    ContactTypeCode, IsBillingContact, IsPortalUser, IsKeyContact, IsServiceContact,
    PreferredContactMethod, ParentContactId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @ContactId, @TenantId, @AccountId, @FirstName, @LastName, @Email, @Phone, @JobTitle,
    @ContactTypeCode, @IsBillingContact, @IsPortalUser, @IsKeyContact, @IsServiceContact,
    @PreferredContactMethod, @ParentContactId, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ContactId = id,
            request.TenantId,
            request.AccountId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.JobTitle,
            request.ContactTypeCode,
            request.IsBillingContact,
            request.IsPortalUser,
            request.IsKeyContact,
            request.IsServiceContact,
            request.PreferredContactMethod,
            request.ParentContactId,
            request.StatusCode,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<ContactDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.ContactId, c.TenantId, c.AccountId, a.AccountName,
       c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
       c.ContactTypeCode, c.IsBillingContact, c.IsPortalUser,
       c.IsKeyContact, c.IsServiceContact, c.ParentContactId, c.PreferredContactMethod,
       c.StatusCode, c.CreatedDateUtc
FROM Client.Contact c
LEFT JOIN Client.Account a ON a.AccountId = c.AccountId
WHERE c.ContactId = @Id AND c.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ContactDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ContactDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT c.ContactId, c.TenantId, c.AccountId, a.AccountName,
           c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
           c.ContactTypeCode, c.IsBillingContact, c.IsPortalUser,
           c.IsKeyContact, c.IsServiceContact, c.ParentContactId, c.PreferredContactMethod,
           c.StatusCode, c.CreatedDateUtc
    FROM Client.Contact c
    LEFT JOIN Client.Account a ON a.AccountId = c.AccountId
    WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR c.LastName LIKE '%' + @SearchTerm + '%'
           OR c.FirstName LIKE '%' + @SearchTerm + '%'
           OR c.Email LIKE '%' + @SearchTerm + '%'
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.Contact c
WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR c.LastName LIKE '%' + @SearchTerm + '%'
       OR c.FirstName LIKE '%' + @SearchTerm + '%'
       OR c.Email LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<ContactDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<ContactDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<ContactDto>> GetByAccountIdAsync(Guid accountId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT c.ContactId, c.TenantId, c.AccountId, a.AccountName,
           c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
           c.ContactTypeCode, c.IsBillingContact, c.IsPortalUser,
           c.IsKeyContact, c.IsServiceContact, c.ParentContactId, c.PreferredContactMethod,
           c.StatusCode, c.CreatedDateUtc
    FROM Client.Contact c
    LEFT JOIN Client.Account a ON a.AccountId = c.AccountId
    WHERE c.AccountId = @AccountId AND c.IsDeleted = 0
)
SELECT * FROM Paged
ORDER BY LastName, FirstName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.Contact WHERE AccountId = @AccountId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            AccountId = accountId,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<ContactDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<ContactDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(Guid id, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.Contact
SET FirstName = @FirstName,
    LastName = @LastName,
    Email = @Email,
    Phone = @Phone,
    JobTitle = @JobTitle,
    ContactTypeCode = @ContactTypeCode,
    IsBillingContact = @IsBillingContact,
    IsPortalUser = @IsPortalUser,
    IsKeyContact = @IsKeyContact,
    IsServiceContact = @IsServiceContact,
    PreferredContactMethod = @PreferredContactMethod,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ContactId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.JobTitle,
            request.ContactTypeCode,
            request.IsBillingContact,
            request.IsPortalUser,
            request.IsKeyContact,
            request.IsServiceContact,
            request.PreferredContactMethod,
            request.StatusCode,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.Contact
SET IsDeleted = 1,
    StatusCode = 'Inactive',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
WHERE ContactId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }
}
