using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ArAgingSnapshotRepository : IArAgingSnapshotRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ArAgingSnapshotRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.ArAgingSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.ArAgingSnapshot
    (
        SnapshotId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        AccountId         UNIQUEIDENTIFIER NOT NULL,
        SnapshotDate      DATE             NOT NULL,
        CurrentAmount     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        Days30Amount      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        Days60Amount      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        Days90Amount      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        Days90PlusAmount  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        TotalOutstanding  DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId   UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc   DATETIME2        NULL,
        ModifiedByUserId  UNIQUEIDENTIFIER NULL,
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'TenantId') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ArAgingSnapshot_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'AccountId') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ArAgingSnapshot_AccountId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'SnapshotDate') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD SnapshotDate DATE NOT NULL CONSTRAINT DF_ArAgingSnapshot_SnapshotDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'CurrentAmount') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD CurrentAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ArAgingSnapshot_CurrentAmount DEFAULT 0;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'Days30Amount') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD Days30Amount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ArAgingSnapshot_Days30Amount DEFAULT 0;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'Days60Amount') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD Days60Amount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ArAgingSnapshot_Days60Amount DEFAULT 0;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'Days90Amount') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD Days90Amount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ArAgingSnapshot_Days90Amount DEFAULT 0;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'Days90PlusAmount') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD Days90PlusAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ArAgingSnapshot_Days90PlusAmount DEFAULT 0;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'TotalOutstanding') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD TotalOutstanding DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ArAgingSnapshot_TotalOutstanding DEFAULT 0;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ArAgingSnapshot_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'CreatedByUserId') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.ArAgingSnapshot', N'IsDeleted') IS NULL ALTER TABLE Billing.ArAgingSnapshot ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ArAgingSnapshot_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.ArAgingSnapshot') AND name = N'IX_ArAgingSnapshot_Tenant_Date')
    CREATE INDEX IX_ArAgingSnapshot_Tenant_Date ON Billing.ArAgingSnapshot(TenantId, SnapshotDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Billing.ArAgingSnapshot WHERE TenantId = @TenantId AND IsDeleted = 0)
   AND OBJECT_ID(N'Billing.Invoice', N'U') IS NOT NULL
BEGIN
    INSERT INTO Billing.ArAgingSnapshot (SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc, IsDeleted)
    SELECT NEWID(),
           i.TenantId,
           i.AccountId,
           CONVERT(date, SYSUTCDATETIME()),
           SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NULL OR DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) <= 0 THEN i.BalanceAmount ELSE 0 END),
           SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) BETWEEN 1 AND 30 THEN i.BalanceAmount ELSE 0 END),
           SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) BETWEEN 31 AND 60 THEN i.BalanceAmount ELSE 0 END),
           SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) BETWEEN 61 AND 90 THEN i.BalanceAmount ELSE 0 END),
           SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) > 90 THEN i.BalanceAmount ELSE 0 END),
           SUM(i.BalanceAmount),
           SYSUTCDATETIME(),
           0
    FROM Billing.Invoice i
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.BalanceAmount > 0
    GROUP BY i.TenantId, i.AccountId;
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<ArAgingSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "SELECT SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc FROM Billing.ArAgingSnapshot WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ArAgingSnapshotDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ArAgingSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Billing.ArAgingSnapshot", "SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc", "CAST(AccountId AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'", "SnapshotDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ArAgingSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ArAgingSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateArAgingSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var total = request.CurrentAmount + request.Days30Amount + request.Days60Amount + request.Days90Amount + request.Days90PlusAmount;
        const string sql = @"
INSERT INTO Billing.ArAgingSnapshot (SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @SnapshotDate, @CurrentAmount, @Days30Amount, @Days60Amount, @Days90Amount, @Days90PlusAmount, @TotalOutstanding, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.SnapshotDate, request.CurrentAmount, request.Days30Amount, request.Days60Amount, request.Days90Amount, request.Days90PlusAmount, TotalOutstanding = total, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateArAgingSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        var total = request.CurrentAmount + request.Days30Amount + request.Days60Amount + request.Days90Amount + request.Days90PlusAmount;
        const string sql = @"
UPDATE Billing.ArAgingSnapshot
SET AccountId = @AccountId,
    SnapshotDate = @SnapshotDate,
    CurrentAmount = @CurrentAmount,
    Days30Amount = @Days30Amount,
    Days60Amount = @Days60Amount,
    Days90Amount = @Days90Amount,
    Days90PlusAmount = @Days90PlusAmount,
    TotalOutstanding = @TotalOutstanding,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.SnapshotDate, request.CurrentAmount, request.Days30Amount, request.Days60Amount, request.Days90Amount, request.Days90PlusAmount, TotalOutstanding = total, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "UPDATE Billing.ArAgingSnapshot SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<int> SyncFromInvoicesAsync(Guid tenantId, DateOnly snapshotDate, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        const string sql = @"
UPDATE Billing.ArAgingSnapshot
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId
WHERE TenantId = @TenantId AND SnapshotDate = @SnapshotDate AND IsDeleted = 0;

INSERT INTO Billing.ArAgingSnapshot (SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(),
       i.TenantId,
       i.AccountId,
       @SnapshotDate,
       SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NULL OR DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) <= 0 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) BETWEEN 1 AND 30 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) BETWEEN 31 AND 60 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) BETWEEN 61 AND 90 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL AND DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) > 90 THEN i.BalanceAmount ELSE 0 END),
       SUM(i.BalanceAmount),
       SYSUTCDATETIME(),
       @CreatedByUserId,
       0
FROM Billing.Invoice i
WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.BalanceAmount > 0
GROUP BY i.TenantId, i.AccountId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, SnapshotDate = snapshotDate, CreatedByUserId = createdByUserId }, cancellationToken: cancellationToken));
    }
}
