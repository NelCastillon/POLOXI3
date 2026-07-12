using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DeferredRevenueRecognitionRepository : IDeferredRevenueRecognitionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DeferredRevenueRecognitionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Finance')
    EXEC('CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.JournalEntry', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.JournalEntry
    (
        JournalEntryId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        EntryNumber     NVARCHAR(50)     NOT NULL,
        EntryDate       DATE             NOT NULL,
        Description     NVARCHAR(1000)   NOT NULL,
        TotalDebit      DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        TotalCredit     DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        StatusCode      NVARCHAR(50)     NOT NULL DEFAULT N'Draft',
        CreatedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2        NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted       BIT              NOT NULL DEFAULT 0
    );
END;

IF OBJECT_ID(N'Finance.JournalEntryLine', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.JournalEntryLine
    (
        LineId uniqueidentifier NOT NULL CONSTRAINT PK_JournalEntryLine PRIMARY KEY,
        JournalEntryId uniqueidentifier NOT NULL,
        GLAccountId uniqueidentifier NOT NULL,
        DebitAmount decimal(18,2) NOT NULL CONSTRAINT DF_JournalEntryLine_Debit DEFAULT (0),
        CreditAmount decimal(18,2) NOT NULL CONSTRAINT DF_JournalEntryLine_Credit DEFAULT (0),
        Description nvarchar(1000) NULL,
        LineOrder int NOT NULL CONSTRAINT DF_JournalEntryLine_LineOrder DEFAULT (0)
    );
END;

IF OBJECT_ID('Finance.DeferredRevenueRecognition', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.DeferredRevenueRecognition
    (
        RecognitionId uniqueidentifier NOT NULL CONSTRAINT PK_DeferredRevenueRecognition PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        DeferredRevenueScheduleId uniqueidentifier NOT NULL,
        RecognitionDate date NOT NULL,
        Amount decimal(18,2) NOT NULL CONSTRAINT DF_DeferredRevenueRecognition_Amount DEFAULT (0),
        JournalEntryId uniqueidentifier NULL,
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_DeferredRevenueRecognition_Status DEFAULT ('Pending'),
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_DeferredRevenueRecognition_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_DeferredRevenueRecognition_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'TenantId') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'DeferredRevenueScheduleId') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD DeferredRevenueScheduleId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'RecognitionDate') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD RecognitionDate date NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'Amount') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD Amount decimal(18,2) NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'JournalEntryId') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD JournalEntryId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'StatusCode') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'CreatedByUserId') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueRecognition', 'IsDeleted') IS NULL ALTER TABLE Finance.DeferredRevenueRecognition ADD IsDeleted bit NULL;

UPDATE Finance.DeferredRevenueRecognition SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.DeferredRevenueRecognition SET RecognitionDate = COALESCE(RecognitionDate, CAST(SYSUTCDATETIME() AS date)) WHERE RecognitionDate IS NULL;
UPDATE Finance.DeferredRevenueRecognition SET Amount = COALESCE(Amount, 0) WHERE Amount IS NULL;
UPDATE Finance.DeferredRevenueRecognition SET StatusCode = COALESCE(NULLIF(StatusCode, ''), 'Pending') WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.DeferredRevenueRecognition SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.DeferredRevenueRecognition SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeferredRevenueRecognition_Tenant_Status' AND object_id = OBJECT_ID('Finance.DeferredRevenueRecognition'))
    CREATE INDEX IX_DeferredRevenueRecognition_Tenant_Status ON Finance.DeferredRevenueRecognition (TenantId, StatusCode, RecognitionDate DESC) INCLUDE (Amount, DeferredRevenueScheduleId, JournalEntryId);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF OBJECT_ID('Finance.DeferredRevenueSchedule', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Finance.DeferredRevenueRecognition WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    DECLARE @Seed TABLE (RowNum int identity(1,1), DeferredRevenueScheduleId uniqueidentifier, Amount decimal(18,2), StatusCode nvarchar(50));

    INSERT INTO @Seed (DeferredRevenueScheduleId, Amount, StatusCode)
    SELECT TOP (6)
           DeferredRevenueScheduleId,
           CASE WHEN RemainingAmount > 0 THEN CAST(RemainingAmount / 6.0 AS decimal(18,2)) ELSE CAST(TotalAmount / 12.0 AS decimal(18,2)) END,
           CASE WHEN StatusCode = 'Completed' THEN 'Posted' ELSE 'Pending' END
    FROM Finance.DeferredRevenueSchedule
    WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0
    ORDER BY StartDate DESC;

    INSERT INTO Finance.DeferredRevenueRecognition (RecognitionId, TenantId, DeferredRevenueScheduleId, RecognitionDate, Amount, JournalEntryId, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, DeferredRevenueScheduleId,
           DATEADD(month, RowNum - 1, DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)),
           CASE WHEN Amount <= 0 THEN 1000 ELSE Amount END,
           NULL,
           CASE WHEN RowNum % 3 = 0 THEN 'Posted' WHEN RowNum % 2 = 0 THEN 'Approved' ELSE StatusCode END,
           SYSUTCDATETIME(), NULL, NULL, NULL, 0
    FROM @Seed;
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<DeferredRevenueRecognitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = "SELECT RecognitionId, TenantId, DeferredRevenueScheduleId, RecognitionDate, Amount, JournalEntryId, StatusCode, CreatedDateUtc FROM Finance.DeferredRevenueRecognition WHERE RecognitionId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DeferredRevenueRecognitionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DeferredRevenueRecognitionDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "RecognitionId, TenantId, DeferredRevenueScheduleId, RecognitionDate, Amount, JournalEntryId, StatusCode, CreatedDateUtc";
        var searchPredicate = "StatusCode LIKE '%' + @SearchTerm + '%'";
        var searchSql = RepositorySql.BuildPagedSearchSql("Finance.DeferredRevenueRecognition", selectColumns, searchPredicate, "RecognitionDate DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DeferredRevenueRecognitionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DeferredRevenueRecognitionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateDeferredRevenueRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.DeferredRevenueRecognition (RecognitionId, TenantId, DeferredRevenueScheduleId, RecognitionDate, Amount, JournalEntryId, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @DeferredRevenueScheduleId, @RecognitionDate, @Amount, @JournalEntryId, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.DeferredRevenueScheduleId, request.RecognitionDate, request.Amount, request.JournalEntryId, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        if (string.Equals(request.StatusCode, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            await PostRecognitionAsync(cn, id, request.TenantId, request.CreatedByUserId, cancellationToken);
        }
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateDeferredRevenueRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.DeferredRevenueRecognition
SET DeferredRevenueScheduleId = @DeferredRevenueScheduleId,
    RecognitionDate = @RecognitionDate,
    Amount = @Amount,
    JournalEntryId = @JournalEntryId,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE RecognitionId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.DeferredRevenueScheduleId, request.RecognitionDate, request.Amount, request.JournalEntryId, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        if (string.Equals(request.StatusCode, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            await PostRecognitionAsync(cn, id, request.TenantId, request.ModifiedByUserId, cancellationToken);
        }
    }

    private static async Task PostRecognitionAsync(System.Data.IDbConnection connection, Guid recognitionId, Guid tenantId, Guid? userId, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @ExistingJournalEntryId uniqueidentifier;
DECLARE @DeferredRevenueScheduleId uniqueidentifier;
DECLARE @RecognitionDate date;
DECLARE @Amount decimal(18,2);
DECLARE @RevenueGLAccountId uniqueidentifier;
DECLARE @DeferredGLAccountId uniqueidentifier;
DECLARE @EntryNumber nvarchar(50);
DECLARE @Description nvarchar(1000);
DECLARE @JournalEntryId uniqueidentifier;

SELECT @ExistingJournalEntryId = r.JournalEntryId,
       @DeferredRevenueScheduleId = r.DeferredRevenueScheduleId,
       @RecognitionDate = r.RecognitionDate,
       @Amount = r.Amount,
       @RevenueGLAccountId = s.GLAccountId,
       @DeferredGLAccountId = s.DeferredGLAccountId
FROM Finance.DeferredRevenueRecognition r
JOIN Finance.DeferredRevenueSchedule s ON s.DeferredRevenueScheduleId = r.DeferredRevenueScheduleId
WHERE r.RecognitionId = @RecognitionId
  AND r.TenantId = @TenantId
  AND COALESCE(r.IsDeleted, 0) = 0
  AND COALESCE(s.IsDeleted, 0) = 0;

IF @DeferredRevenueScheduleId IS NULL
    THROW 51001, 'Deferred revenue recognition or schedule was not found.', 1;

IF @ExistingJournalEntryId IS NOT NULL
    RETURN;

IF @Amount <= 0
    THROW 51002, 'Deferred revenue recognition amount must be greater than zero before posting.', 1;

IF @RevenueGLAccountId IS NULL OR @DeferredGLAccountId IS NULL
    THROW 51003, 'Deferred revenue schedule requires both revenue and deferred GL accounts before posting.', 1;

IF NOT EXISTS (SELECT 1 FROM Finance.GLAccount WHERE GLAccountId = @RevenueGLAccountId AND TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
    THROW 51004, 'Revenue GL account was not found for the deferred revenue schedule.', 1;

IF NOT EXISTS (SELECT 1 FROM Finance.GLAccount WHERE GLAccountId = @DeferredGLAccountId AND TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
    THROW 51005, 'Deferred revenue GL account was not found for the deferred revenue schedule.', 1;

SET @JournalEntryId = NEWID();
SET @EntryNumber = CONCAT(N'DRR-', FORMAT(@RecognitionDate, 'yyyyMMdd'), N'-', LEFT(CONVERT(nvarchar(36), @RecognitionId), 8));
SET @Description = CONCAT(N'Deferred revenue recognition ', LEFT(CONVERT(nvarchar(36), @RecognitionId), 8));

BEGIN TRANSACTION;

INSERT INTO Finance.JournalEntry (JournalEntryId, TenantId, EntryNumber, EntryDate, Description, TotalDebit, TotalCredit, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@JournalEntryId, @TenantId, @EntryNumber, @RecognitionDate, @Description, @Amount, @Amount, N'Posted', SYSUTCDATETIME(), @UserId, NULL, NULL, 0);

INSERT INTO Finance.JournalEntryLine (LineId, JournalEntryId, GLAccountId, DebitAmount, CreditAmount, Description, LineOrder)
VALUES (NEWID(), @JournalEntryId, @DeferredGLAccountId, @Amount, 0, N'Debit deferred revenue liability', 1),
       (NEWID(), @JournalEntryId, @RevenueGLAccountId, 0, @Amount, N'Credit recognized revenue', 2);

UPDATE Finance.DeferredRevenueRecognition
SET JournalEntryId = @JournalEntryId,
    StatusCode = N'Posted',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
WHERE RecognitionId = @RecognitionId
  AND TenantId = @TenantId
  AND COALESCE(IsDeleted, 0) = 0;

UPDATE s
SET RecognizedAmount = CASE WHEN s.RecognizedAmount + @Amount > s.TotalAmount THEN s.TotalAmount ELSE s.RecognizedAmount + @Amount END,
    RemainingAmount = CASE WHEN s.TotalAmount - (s.RecognizedAmount + @Amount) < 0 THEN 0 ELSE s.TotalAmount - (s.RecognizedAmount + @Amount) END,
    StatusCode = CASE WHEN s.TotalAmount - (s.RecognizedAmount + @Amount) <= 0 THEN N'Completed' ELSE s.StatusCode END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
FROM Finance.DeferredRevenueSchedule s
WHERE s.DeferredRevenueScheduleId = @DeferredRevenueScheduleId
  AND s.TenantId = @TenantId
  AND COALESCE(s.IsDeleted, 0) = 0;

COMMIT TRANSACTION;";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { RecognitionId = recognitionId, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }
}
