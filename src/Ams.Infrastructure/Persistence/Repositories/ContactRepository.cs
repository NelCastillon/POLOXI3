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

    public async Task<IReadOnlyList<ContactWorkflowEventDto>> GetWorkflowEventsAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT WorkflowEventId, TenantId, ContactId, EventType, EventTitle, EventDetail,
       RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId
FROM Client.ContactWorkflowEvent
WHERE ContactId = @ContactId AND IsDeleted = 0
ORDER BY EventDateUtc DESC, CreatedDateUtc DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureWorkflowEventTableAsync(cn, cancellationToken);
        var items = await cn.QueryAsync<ContactWorkflowEventDto>(new CommandDefinition(sql, new { ContactId = contactId }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<Guid> CreateWorkflowEventAsync(CreateContactWorkflowEventRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Client.ContactWorkflowEvent
(
    WorkflowEventId, TenantId, ContactId, EventType, EventTitle, EventDetail,
    RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @WorkflowEventId, @TenantId, @ContactId, @EventType, @EventTitle, @EventDetail,
    @RelatedEntityName, @RelatedEntityId, COALESCE(@EventDateUtc, SYSUTCDATETIME()), SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureWorkflowEventTableAsync(cn, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowEventId = id,
            request.TenantId,
            request.ContactId,
            request.EventType,
            request.EventTitle,
            request.EventDetail,
            request.RelatedEntityName,
            request.RelatedEntityId,
            request.EventDateUtc,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    private static Task EnsureWorkflowEventTableAsync(System.Data.IDbConnection cn, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Client')
    EXEC(N'CREATE SCHEMA Client');

IF OBJECT_ID(N'Client.ContactWorkflowEvent', N'U') IS NULL
BEGIN
    CREATE TABLE Client.ContactWorkflowEvent
    (
        WorkflowEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ContactWorkflowEvent PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ContactId UNIQUEIDENTIFIER NOT NULL,
        EventType NVARCHAR(50) NOT NULL,
        EventTitle NVARCHAR(200) NOT NULL,
        EventDetail NVARCHAR(1000) NULL,
        RelatedEntityName NVARCHAR(100) NULL,
        RelatedEntityId UNIQUEIDENTIFIER NULL,
        EventDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ContactWorkflowEvent_Date DEFAULT SYSUTCDATETIME(),
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ContactWorkflowEvent_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_ContactWorkflowEvent_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Client.ContactWorkflowEvent', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ContactWorkflowEvent_ContactId_Date' AND object_id = OBJECT_ID(N'Client.ContactWorkflowEvent'))
    CREATE NONCLUSTERED INDEX IX_ContactWorkflowEvent_ContactId_Date ON Client.ContactWorkflowEvent(ContactId, IsDeleted, EventDateUtc DESC, CreatedDateUtc DESC);";

        return cn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
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
