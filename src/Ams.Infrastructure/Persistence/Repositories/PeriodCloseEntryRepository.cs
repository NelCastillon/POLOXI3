using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PeriodCloseEntryRepository : IPeriodCloseEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PeriodCloseEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Finance')
    EXEC('CREATE SCHEMA Finance');

IF OBJECT_ID('Finance.PeriodCloseEntry', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.PeriodCloseEntry
    (
        PeriodCloseEntryId uniqueidentifier NOT NULL CONSTRAINT PK_PeriodCloseEntry PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        AccountingPeriodId uniqueidentifier NOT NULL,
        TaskDescription nvarchar(500) NOT NULL,
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_PeriodCloseEntry_Status DEFAULT ('Open'),
        CompletedByUserId uniqueidentifier NULL,
        CompletedDateUtc datetime2(7) NULL,
        Notes nvarchar(1000) NULL,
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_PeriodCloseEntry_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_PeriodCloseEntry_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH('Finance.PeriodCloseEntry', 'TenantId') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'AccountingPeriodId') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD AccountingPeriodId uniqueidentifier NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'TaskDescription') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD TaskDescription nvarchar(500) NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'StatusCode') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'CompletedByUserId') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD CompletedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'CompletedDateUtc') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD CompletedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'Notes') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'CreatedByUserId') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.PeriodCloseEntry', 'IsDeleted') IS NULL ALTER TABLE Finance.PeriodCloseEntry ADD IsDeleted bit NULL;

UPDATE Finance.PeriodCloseEntry SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.PeriodCloseEntry SET TaskDescription = COALESCE(NULLIF(TaskDescription, ''), 'Period close checklist item') WHERE TaskDescription IS NULL OR TaskDescription = '';
UPDATE Finance.PeriodCloseEntry SET StatusCode = COALESCE(NULLIF(StatusCode, ''), 'Open') WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.PeriodCloseEntry SET CompletedDateUtc = COALESCE(CompletedDateUtc, SYSUTCDATETIME()) WHERE StatusCode = 'Completed' AND CompletedDateUtc IS NULL;
UPDATE Finance.PeriodCloseEntry SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.PeriodCloseEntry SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF OBJECT_ID('Finance.AccountingPeriod', 'U') IS NOT NULL
BEGIN
    UPDATE pce
    SET AccountingPeriodId = ap.AccountingPeriodId
    FROM Finance.PeriodCloseEntry pce
    CROSS APPLY (
        SELECT TOP (1) AccountingPeriodId
        FROM Finance.AccountingPeriod ap
        WHERE (@TenantId IS NULL OR ap.TenantId = COALESCE(pce.TenantId, @TenantId))
          AND COALESCE(ap.IsDeleted, 0) = 0
        ORDER BY CASE WHEN ap.StartDate <= CAST(SYSUTCDATETIME() AS date) AND ap.EndDate >= CAST(SYSUTCDATETIME() AS date) THEN 0 ELSE 1 END, ap.StartDate DESC
    ) ap
    WHERE pce.AccountingPeriodId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PeriodCloseEntry_Tenant_Period' AND object_id = OBJECT_ID('Finance.PeriodCloseEntry'))
    CREATE INDEX IX_PeriodCloseEntry_Tenant_Period ON Finance.PeriodCloseEntry (TenantId, AccountingPeriodId, CreatedDateUtc DESC) INCLUDE (TaskDescription, StatusCode, CompletedDateUtc);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF OBJECT_ID('Finance.AccountingPeriod', 'U') IS NOT NULL
BEGIN
    DECLARE @AccountingPeriodId uniqueidentifier;
    SELECT TOP (1) @AccountingPeriodId = AccountingPeriodId
    FROM Finance.AccountingPeriod
    WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0
    ORDER BY CASE WHEN StartDate <= CAST(SYSUTCDATETIME() AS date) AND EndDate >= CAST(SYSUTCDATETIME() AS date) THEN 0 ELSE 1 END, StartDate DESC;

    IF @AccountingPeriodId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Finance.PeriodCloseEntry WHERE TenantId = @TenantId AND AccountingPeriodId = @AccountingPeriodId AND COALESCE(IsDeleted, 0) = 0)
    BEGIN
        INSERT INTO Finance.PeriodCloseEntry (PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
        VALUES
        (NEWID(), @TenantId, @AccountingPeriodId, 'Review unposted journal entries', 'Open', NULL, NULL, 'Confirm all journal entries for the period are posted or intentionally deferred.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @AccountingPeriodId, 'Complete bank reconciliation', 'Open', NULL, NULL, 'Verify cash accounts agree to bank statements before closing.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @AccountingPeriodId, 'Validate AP and AR cutoff', 'Open', NULL, NULL, 'Confirm invoices, payments, receipts, and adjustments are recorded in the proper period.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @AccountingPeriodId, 'Generate trial balance review', 'Open', NULL, NULL, 'Review trial balance variances and investigate unexpected movements.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @AccountingPeriodId, 'Approve period close package', 'Blocked', NULL, NULL, 'Final approval remains blocked until all close tasks are completed.', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
    END;
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<PeriodCloseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = "SELECT PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc FROM Finance.PeriodCloseEntry WHERE PeriodCloseEntryId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PeriodCloseEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PeriodCloseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc";
        var searchPredicate = "TaskDescription LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%'";
        var searchSql = RepositorySql.BuildPagedSearchSql("Finance.PeriodCloseEntry", selectColumns, searchPredicate, "CreatedDateUtc DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PeriodCloseEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PeriodCloseEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.PeriodCloseEntry (PeriodCloseEntryId, TenantId, AccountingPeriodId, TaskDescription, StatusCode, CompletedByUserId, CompletedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountingPeriodId, @TaskDescription, @StatusCode, @CompletedByUserId, CASE WHEN @StatusCode = 'Completed' THEN COALESCE(@CompletedDateUtc, SYSUTCDATETIME()) ELSE @CompletedDateUtc END, @Notes, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountingPeriodId, request.TaskDescription, request.StatusCode, request.CompletedByUserId, request.CompletedDateUtc, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.PeriodCloseEntry
SET AccountingPeriodId = @AccountingPeriodId,
    TaskDescription = @TaskDescription,
    StatusCode = @StatusCode,
    CompletedByUserId = @CompletedByUserId,
    CompletedDateUtc = CASE WHEN @StatusCode = 'Completed' THEN COALESCE(@CompletedDateUtc, CompletedDateUtc, SYSUTCDATETIME()) ELSE @CompletedDateUtc END,
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE PeriodCloseEntryId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountingPeriodId, request.TaskDescription, request.StatusCode, request.CompletedByUserId, request.CompletedDateUtc, request.Notes, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
