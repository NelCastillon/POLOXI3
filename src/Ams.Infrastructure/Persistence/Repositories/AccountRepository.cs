using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Client.Account
(
    AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
    MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
    ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
    CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @AccountId, @TenantId, @AccountNumber, @AccountName, @AccountTypeCode,
    @MainEmail, @MainPhone, @StatusCode, @SegmentCode, @OwnerUserId,
    @ParentAccountId, @LifecycleStageCode, @Industry, @Website, @AnnualRevenue,
    SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AccountId = id,
            request.TenantId,
            request.AccountNumber,
            request.AccountName,
            request.AccountTypeCode,
            request.MainEmail,
            request.MainPhone,
            request.StatusCode,
            request.SegmentCode,
            request.OwnerUserId,
            request.ParentAccountId,
            request.LifecycleStageCode,
            request.Industry,
            request.Website,
            request.AnnualRevenue,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
       MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
       ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
       CreatedDateUtc, ModifiedDateUtc
FROM Client.Account
WHERE AccountId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
           MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
           ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
           CreatedDateUtc, ModifiedDateUtc
    FROM Client.Account
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR AccountName LIKE '%' + @SearchTerm + '%'
           OR AccountNumber LIKE '%' + @SearchTerm + '%'
           OR MainEmail LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.Account
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR AccountName LIKE '%' + @SearchTerm + '%'
       OR AccountNumber LIKE '%' + @SearchTerm + '%'
       OR MainEmail LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AccountDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AccountDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.Account
SET AccountName = @AccountName,
    AccountTypeCode = @AccountTypeCode,
    MainEmail = @MainEmail,
    MainPhone = @MainPhone,
    StatusCode = @StatusCode,
    SegmentCode = @SegmentCode,
    OwnerUserId = @OwnerUserId,
    ParentAccountId = @ParentAccountId,
    LifecycleStageCode = @LifecycleStageCode,
    Industry = @Industry,
    Website = @Website,
    AnnualRevenue = @AnnualRevenue,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AccountId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.AccountName,
            request.AccountTypeCode,
            request.MainEmail,
            request.MainPhone,
            request.StatusCode,
            request.SegmentCode,
            request.OwnerUserId,
            request.ParentAccountId,
            request.LifecycleStageCode,
            request.Industry,
            request.Website,
            request.AnnualRevenue,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.Account
SET IsDeleted = 1,
    StatusCode = 'Inactive',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
WHERE AccountId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.ContactId, c.TenantId, c.AccountId, a.AccountName,
       c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
       c.ContactTypeCode, c.IsBillingContact, c.IsPortalUser,
       c.IsKeyContact, c.IsServiceContact, c.ParentContactId, c.PreferredContactMethod,
       c.StatusCode, c.CreatedDateUtc
FROM Client.Contact c
LEFT JOIN Client.Account a ON a.AccountId = c.AccountId
WHERE c.AccountId = @AccountId AND c.IsDeleted = 0
ORDER BY c.LastName, c.FirstName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await cn.QueryAsync<ContactDto>(
            new CommandDefinition(sql, new { AccountId = accountId }, cancellationToken: cancellationToken));
        return results.AsList();
    }

    public async Task<IReadOnlyList<AccountDto>> FindMatchCandidatesAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 50 AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
       MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
       ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
       CreatedDateUtc, ModifiedDateUtc
FROM Client.Account
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (
        (@BusinessName IS NOT NULL AND @BusinessName <> '' AND AccountName LIKE '%' + @BusinessName + '%')
     OR (@Email IS NOT NULL AND @Email <> '' AND MainEmail = @Email)
     OR (@Phone IS NOT NULL AND @Phone <> '' AND MainPhone = @Phone)
  )
ORDER BY AccountName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await cn.QueryAsync<AccountDto>(new CommandDefinition(sql, new
        {
            criteria.TenantId,
            criteria.BusinessName,
            criteria.Email,
            criteria.Phone
        }, cancellationToken: cancellationToken));
        return results.AsList();
    }
}
