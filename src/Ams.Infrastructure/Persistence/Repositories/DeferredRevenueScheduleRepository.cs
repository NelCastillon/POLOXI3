using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DeferredRevenueScheduleRepository : IDeferredRevenueScheduleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DeferredRevenueScheduleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Finance')
    EXEC('CREATE SCHEMA Finance');

IF OBJECT_ID('Finance.DeferredRevenueSchedule', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.DeferredRevenueSchedule
    (
        DeferredRevenueScheduleId uniqueidentifier NOT NULL CONSTRAINT PK_DeferredRevenueSchedule PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        AccountId uniqueidentifier NOT NULL,
        InvoiceId uniqueidentifier NULL,
        AgreementId uniqueidentifier NULL,
        TotalAmount decimal(18,2) NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_Total DEFAULT (0),
        RecognizedAmount decimal(18,2) NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_Recognized DEFAULT (0),
        RemainingAmount decimal(18,2) NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_Remaining DEFAULT (0),
        StartDate date NOT NULL,
        EndDate date NULL,
        FrequencyCode nvarchar(50) NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_Frequency DEFAULT ('Monthly'),
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_Status DEFAULT ('Active'),
        GLAccountId uniqueidentifier NULL,
        DeferredGLAccountId uniqueidentifier NULL,
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_DeferredRevenueSchedule_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'TenantId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'AccountId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD AccountId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'InvoiceId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD InvoiceId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'AgreementId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD AgreementId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'TotalAmount') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD TotalAmount decimal(18,2) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'RecognizedAmount') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD RecognizedAmount decimal(18,2) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'RemainingAmount') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD RemainingAmount decimal(18,2) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'StartDate') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD StartDate date NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'EndDate') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD EndDate date NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'FrequencyCode') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD FrequencyCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'StatusCode') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'GLAccountId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD GLAccountId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'DeferredGLAccountId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD DeferredGLAccountId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'CreatedByUserId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.DeferredRevenueSchedule', 'IsDeleted') IS NULL ALTER TABLE Finance.DeferredRevenueSchedule ADD IsDeleted bit NULL;

UPDATE Finance.DeferredRevenueSchedule SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.DeferredRevenueSchedule SET TotalAmount = COALESCE(TotalAmount, 0) WHERE TotalAmount IS NULL;
UPDATE Finance.DeferredRevenueSchedule SET RecognizedAmount = COALESCE(RecognizedAmount, 0) WHERE RecognizedAmount IS NULL;
UPDATE Finance.DeferredRevenueSchedule SET RemainingAmount = COALESCE(RemainingAmount, TotalAmount - RecognizedAmount, 0) WHERE RemainingAmount IS NULL;
UPDATE Finance.DeferredRevenueSchedule SET StartDate = COALESCE(StartDate, CAST(SYSUTCDATETIME() AS date)) WHERE StartDate IS NULL;
UPDATE Finance.DeferredRevenueSchedule SET FrequencyCode = COALESCE(NULLIF(FrequencyCode, ''), 'Monthly') WHERE FrequencyCode IS NULL OR FrequencyCode = '';
UPDATE Finance.DeferredRevenueSchedule SET StatusCode = COALESCE(NULLIF(StatusCode, ''), CASE WHEN RemainingAmount <= 0 THEN 'Completed' ELSE 'Active' END) WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.DeferredRevenueSchedule SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.DeferredRevenueSchedule SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF OBJECT_ID('Client.Account', 'U') IS NOT NULL
BEGIN
    UPDATE drs
    SET AccountId = a.AccountId
    FROM Finance.DeferredRevenueSchedule drs
    CROSS APPLY (
        SELECT TOP (1) AccountId
        FROM Client.Account a
        WHERE (@TenantId IS NULL OR a.TenantId = COALESCE(drs.TenantId, @TenantId))
          AND COALESCE(a.IsDeleted, 0) = 0
        ORDER BY a.CreatedDateUtc DESC
    ) a
    WHERE drs.AccountId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeferredRevenueSchedule_Tenant_Status' AND object_id = OBJECT_ID('Finance.DeferredRevenueSchedule'))
    CREATE INDEX IX_DeferredRevenueSchedule_Tenant_Status ON Finance.DeferredRevenueSchedule (TenantId, StatusCode, StartDate DESC) INCLUDE (TotalAmount, RecognizedAmount, RemainingAmount, FrequencyCode);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF OBJECT_ID('Client.Account', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Finance.DeferredRevenueSchedule WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    DECLARE @Seed TABLE (RowNum int identity(1,1), AccountId uniqueidentifier);
    INSERT INTO @Seed (AccountId)
    SELECT TOP (5) AccountId
    FROM Client.Account
    WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0
    ORDER BY CreatedDateUtc DESC;

    IF EXISTS (SELECT 1 FROM @Seed)
    BEGIN
        INSERT INTO Finance.DeferredRevenueSchedule (DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
        SELECT NEWID(), @TenantId, AccountId, NULL, NULL,
               CASE RowNum WHEN 1 THEN 24000 WHEN 2 THEN 18000 WHEN 3 THEN 36000 WHEN 4 THEN 12000 ELSE 30000 END,
               CASE RowNum WHEN 1 THEN 6000 WHEN 2 THEN 4500 WHEN 3 THEN 12000 WHEN 4 THEN 12000 ELSE 0 END,
               CASE RowNum WHEN 1 THEN 18000 WHEN 2 THEN 13500 WHEN 3 THEN 24000 WHEN 4 THEN 0 ELSE 30000 END,
               DATEADD(month, -RowNum + 1, DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)),
               DATEADD(day, -1, DATEADD(month, 12 - RowNum + 1, DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1))),
               CASE WHEN RowNum = 5 THEN 'Quarterly' ELSE 'Monthly' END,
               CASE WHEN RowNum = 4 THEN 'Completed' WHEN RowNum = 5 THEN 'Pending' ELSE 'Active' END,
               NULL, NULL, SYSUTCDATETIME(), NULL, NULL, NULL, 0
        FROM @Seed;
    END;
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<DeferredRevenueScheduleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = "SELECT DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc FROM Finance.DeferredRevenueSchedule WHERE DeferredRevenueScheduleId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DeferredRevenueScheduleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DeferredRevenueScheduleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc";
        var searchPredicate = "FrequencyCode LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";
        var searchSql = RepositorySql.BuildPagedSearchSql("Finance.DeferredRevenueSchedule", selectColumns, searchPredicate, "StartDate DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DeferredRevenueScheduleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DeferredRevenueScheduleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        var remainingAmount = Math.Max(0, request.TotalAmount - request.RecognizedAmount);
        const string sql = @"
INSERT INTO Finance.DeferredRevenueSchedule (DeferredRevenueScheduleId, TenantId, AccountId, InvoiceId, AgreementId, TotalAmount, RecognizedAmount, RemainingAmount, StartDate, EndDate, FrequencyCode, StatusCode, GLAccountId, DeferredGLAccountId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @InvoiceId, @AgreementId, @TotalAmount, @RecognizedAmount, @RemainingAmount, @StartDate, @EndDate, @FrequencyCode, @StatusCode, @GLAccountId, @DeferredGLAccountId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.InvoiceId, request.AgreementId, request.TotalAmount, request.RecognizedAmount, RemainingAmount = remainingAmount, request.StartDate, request.EndDate, request.FrequencyCode, request.StatusCode, request.GLAccountId, request.DeferredGLAccountId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var remainingAmount = Math.Max(0, request.TotalAmount - request.RecognizedAmount);
        const string sql = @"
UPDATE Finance.DeferredRevenueSchedule
SET AccountId = @AccountId,
    InvoiceId = @InvoiceId,
    AgreementId = @AgreementId,
    TotalAmount = @TotalAmount,
    RecognizedAmount = @RecognizedAmount,
    RemainingAmount = @RemainingAmount,
    StartDate = @StartDate,
    EndDate = @EndDate,
    FrequencyCode = @FrequencyCode,
    StatusCode = @StatusCode,
    GLAccountId = @GLAccountId,
    DeferredGLAccountId = @DeferredGLAccountId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE DeferredRevenueScheduleId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.InvoiceId, request.AgreementId, request.TotalAmount, request.RecognizedAmount, RemainingAmount = remainingAmount, request.StartDate, request.EndDate, request.FrequencyCode, request.StatusCode, request.GLAccountId, request.DeferredGLAccountId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
