using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountingPeriodRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Finance')
    EXEC('CREATE SCHEMA Finance');

IF OBJECT_ID('Finance.AccountingPeriod', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.AccountingPeriod
    (
        AccountingPeriodId uniqueidentifier NOT NULL CONSTRAINT PK_AccountingPeriod PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        PeriodCode nvarchar(50) NOT NULL,
        PeriodName nvarchar(150) NOT NULL,
        StartDate date NOT NULL,
        EndDate date NOT NULL,
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_AccountingPeriod_Status DEFAULT ('Open'),
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_AccountingPeriod_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_AccountingPeriod_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH('Finance.AccountingPeriod', 'TenantId') IS NULL ALTER TABLE Finance.AccountingPeriod ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'PeriodCode') IS NULL ALTER TABLE Finance.AccountingPeriod ADD PeriodCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'PeriodName') IS NULL ALTER TABLE Finance.AccountingPeriod ADD PeriodName nvarchar(150) NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'StartDate') IS NULL ALTER TABLE Finance.AccountingPeriod ADD StartDate date NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'EndDate') IS NULL ALTER TABLE Finance.AccountingPeriod ADD EndDate date NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'StatusCode') IS NULL ALTER TABLE Finance.AccountingPeriod ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.AccountingPeriod ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'CreatedByUserId') IS NULL ALTER TABLE Finance.AccountingPeriod ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.AccountingPeriod ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.AccountingPeriod ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.AccountingPeriod', 'IsDeleted') IS NULL ALTER TABLE Finance.AccountingPeriod ADD IsDeleted bit NULL;

UPDATE Finance.AccountingPeriod SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.AccountingPeriod SET StartDate = COALESCE(StartDate, DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)) WHERE StartDate IS NULL;
UPDATE Finance.AccountingPeriod SET EndDate = COALESCE(EndDate, EOMONTH(StartDate)) WHERE EndDate IS NULL;
UPDATE Finance.AccountingPeriod SET PeriodCode = COALESCE(NULLIF(PeriodCode, ''), FORMAT(StartDate, 'yyyy-MM')) WHERE PeriodCode IS NULL OR PeriodCode = '';
UPDATE Finance.AccountingPeriod SET PeriodName = COALESCE(NULLIF(PeriodName, ''), CONCAT(DATENAME(month, StartDate), ' ', YEAR(StartDate))) WHERE PeriodName IS NULL OR PeriodName = '';
UPDATE Finance.AccountingPeriod SET StatusCode = COALESCE(NULLIF(StatusCode, ''), 'Open') WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.AccountingPeriod SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.AccountingPeriod SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccountingPeriod_Tenant_Start' AND object_id = OBJECT_ID('Finance.AccountingPeriod'))
    CREATE INDEX IX_AccountingPeriod_Tenant_Start ON Finance.AccountingPeriod (TenantId, StartDate DESC) INCLUDE (PeriodCode, PeriodName, EndDate, StatusCode);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Finance.AccountingPeriod WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    DECLARE @Year int = YEAR(SYSUTCDATETIME());
    DECLARE @Month int = 1;
    WHILE @Month <= 12
    BEGIN
        DECLARE @Start date = DATEFROMPARTS(@Year, @Month, 1);
        DECLARE @End date = EOMONTH(@Start);
        INSERT INTO Finance.AccountingPeriod (AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, FORMAT(@Start, 'yyyy-MM'), CONCAT(DATENAME(month, @Start), ' ', @Year), @Start, @End, CASE WHEN @Month < MONTH(SYSUTCDATETIME()) THEN 'Closed' ELSE 'Open' END, SYSUTCDATETIME(), NULL, NULL, NULL, 0);
        SET @Month += 1;
    END;
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<AccountingPeriodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = @"
SELECT AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, CreatedDateUtc
FROM Finance.AccountingPeriod
WHERE AccountingPeriodId = @Id AND COALESCE(IsDeleted, 0) = 0";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountingPeriodDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountingPeriodDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, CreatedDateUtc";
        var searchPredicate = "PeriodName LIKE '%' + @SearchTerm + '%' OR PeriodCode LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.AccountingPeriod", selectColumns, searchPredicate, "StartDate DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AccountingPeriodDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccountingPeriodDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.AccountingPeriod (AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PeriodCode, @PeriodName, @StartDate, @EndDate, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PeriodCode, request.PeriodName, request.StartDate, request.EndDate, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.AccountingPeriod
SET PeriodCode = @PeriodCode,
    PeriodName = @PeriodName,
    StartDate = @StartDate,
    EndDate = @EndDate,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AccountingPeriodId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.PeriodCode, request.PeriodName, request.StartDate, request.EndDate, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}