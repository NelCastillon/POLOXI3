using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TrialBalanceSnapshotRepository : ITrialBalanceSnapshotRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.TrialBalanceSnapshot",
        "TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "AccountCode LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%'",
        "AccountCode ASC");

    public TrialBalanceSnapshotRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<TrialBalanceSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken)) return null;

        const string sql = "SELECT TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.TrialBalanceSnapshot WHERE TrialBalanceSnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TrialBalanceSnapshotDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TrialBalanceSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            return await SearchDerivedFromGLAccountsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TrialBalanceSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TrialBalanceSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.TrialBalanceSnapshot does not exist in the current database schema. Trial Balance is displayed as a derived read-only view from Finance.GLAccount until a snapshot table is added.");

        var id = Guid.NewGuid();
        var netBalance = request.DebitBalance - request.CreditBalance;
        const string sql = @"
INSERT INTO Finance.TrialBalanceSnapshot (TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @SnapshotDate, @AccountingPeriodId, @GLAccountId, @AccountCode, @AccountName, @DebitBalance, @CreditBalance, @NetBalance, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.SnapshotDate, request.AccountingPeriodId, request.GLAccountId, request.AccountCode, request.AccountName, request.DebitBalance, request.CreditBalance, NetBalance = netBalance, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.TrialBalanceSnapshot does not exist in the current database schema. Trial Balance is displayed as a derived read-only view from Finance.GLAccount until a snapshot table is added.");

        var netBalance = request.DebitBalance - request.CreditBalance;
        const string sql = @"
UPDATE Finance.TrialBalanceSnapshot
SET SnapshotDate = @SnapshotDate,
    AccountingPeriodId = @AccountingPeriodId,
    GLAccountId = @GLAccountId,
    AccountCode = @AccountCode,
    AccountName = @AccountName,
    DebitBalance = @DebitBalance,
    CreditBalance = @CreditBalance,
    NetBalance = @NetBalance
WHERE TrialBalanceSnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.SnapshotDate, request.AccountingPeriodId, request.GLAccountId, request.AccountCode, request.AccountName, request.DebitBalance, request.CreditBalance, NetBalance = netBalance }, cancellationToken: cancellationToken));
    }

    private async Task<bool> TableExistsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'Finance.TrialBalanceSnapshot', N'U') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private async Task<PagedResult<TrialBalanceSnapshotDto>> SearchDerivedFromGLAccountsAsync(Guid tenantId, string? searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = @"
WITH Base AS (
    SELECT
        NEWID() AS TrialBalanceSnapshotId,
        TenantId,
        CAST(SYSUTCDATETIME() AS date) AS SnapshotDate,
        CAST(NULL AS uniqueidentifier) AS AccountingPeriodId,
        GLAccountId,
        AccountCode,
        AccountName,
        CAST(0 AS decimal(18,2)) AS DebitBalance,
        CAST(0 AS decimal(18,2)) AS CreditBalance,
        CAST(0 AS decimal(18,2)) AS NetBalance,
        CreatedDateUtc,
        CAST(NULL AS uniqueidentifier) AS CreatedByUserId,
        IsDeleted
    FROM Finance.GLAccount
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AccountCode LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%')
)
SELECT TrialBalanceSnapshotId, TenantId, SnapshotDate, AccountingPeriodId, GLAccountId, AccountCode, AccountName, DebitBalance, CreditBalance, NetBalance, CreatedDateUtc, CreatedByUserId, IsDeleted
FROM Base
ORDER BY AccountCode ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Finance.GLAccount
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AccountCode LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TrialBalanceSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TrialBalanceSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
