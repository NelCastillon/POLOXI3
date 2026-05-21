using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DelinquencyFlagRepository : IDelinquencyFlagRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public DelinquencyFlagRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.DelinquencyFlag', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.DelinquencyFlag
    (
        DelinquencyFlagId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        AccountId         UNIQUEIDENTIFIER NOT NULL,
        InvoiceId         UNIQUEIDENTIFIER NULL,
        FlagDate          DATE             NOT NULL,
        DaysOverdue       INT              NOT NULL,
        OverdueAmount     DECIMAL(18, 2)   NOT NULL,
        SeverityCode      NVARCHAR(50)     NOT NULL,
        StatusCode        NVARCHAR(50)     NOT NULL DEFAULT N'Open',
        ResolvedDate      DATE             NULL,
        Notes             NVARCHAR(1000)   NULL,
        AssignedToUserId  UNIQUEIDENTIFIER NULL,
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId   UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc   DATETIME2        NULL,
        ModifiedByUserId  UNIQUEIDENTIFIER NULL,
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'TenantId') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DelinquencyFlag_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'AccountId') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DelinquencyFlag_AccountId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'InvoiceId') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD InvoiceId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'FlagDate') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD FlagDate DATE NOT NULL CONSTRAINT DF_DelinquencyFlag_FlagDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'DaysOverdue') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD DaysOverdue INT NOT NULL CONSTRAINT DF_DelinquencyFlag_DaysOverdue DEFAULT 0;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'OverdueAmount') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD OverdueAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_DelinquencyFlag_OverdueAmount DEFAULT 0;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'SeverityCode') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD SeverityCode NVARCHAR(50) NOT NULL CONSTRAINT DF_DelinquencyFlag_SeverityCode DEFAULT N'Low';
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'StatusCode') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_DelinquencyFlag_StatusCode DEFAULT N'Open';
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'ResolvedDate') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD ResolvedDate DATE NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'Notes') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD Notes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'AssignedToUserId') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD AssignedToUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_DelinquencyFlag_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'CreatedByUserId') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.DelinquencyFlag', N'IsDeleted') IS NULL ALTER TABLE Billing.DelinquencyFlag ADD IsDeleted BIT NOT NULL CONSTRAINT DF_DelinquencyFlag_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.DelinquencyFlag') AND name = N'IX_DelinquencyFlag_Tenant_FlagDate')
    CREATE INDEX IX_DelinquencyFlag_Tenant_FlagDate ON Billing.DelinquencyFlag(TenantId, FlagDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Billing.DelinquencyFlag WHERE TenantId = @TenantId AND IsDeleted = 0)
   AND OBJECT_ID(N'Billing.Invoice', N'U') IS NOT NULL
BEGIN
    INSERT INTO Billing.DelinquencyFlag (DelinquencyFlagId, TenantId, AccountId, InvoiceId, FlagDate, DaysOverdue, OverdueAmount, SeverityCode, StatusCode, Notes, CreatedDateUtc, IsDeleted)
    SELECT TOP (12)
        NEWID(),
        i.TenantId,
        i.AccountId,
        i.InvoiceId,
        CONVERT(date, SYSUTCDATETIME()),
        CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL THEN DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) ELSE 30 END,
        i.BalanceAmount,
        CASE
            WHEN (CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL THEN DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) ELSE 30 END) > 90 THEN N'Critical'
            WHEN (CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL THEN DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) ELSE 30 END) > 60 THEN N'High'
            WHEN (CASE WHEN COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NOT NULL THEN DATEDIFF(day, CAST(i.DueDate AS date), CONVERT(date, SYSUTCDATETIME())) ELSE 30 END) > 30 THEN N'Medium'
            ELSE N'Low'
        END,
        N'Open',
        N'Tenant Admin delinquency flag generated from overdue billing invoice data.',
        SYSUTCDATETIME(),
        0
    FROM Billing.Invoice i
    WHERE i.TenantId = @TenantId
      AND i.IsDeleted = 0
      AND i.BalanceAmount > 0
      AND (COL_LENGTH(N'Billing.Invoice', N'DueDate') IS NULL OR CAST(i.DueDate AS date) < CONVERT(date, SYSUTCDATETIME()))
    ORDER BY i.CreatedDateUtc DESC;
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<DelinquencyFlagDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "SELECT DelinquencyFlagId, TenantId, AccountId, InvoiceId, FlagDate, DaysOverdue, OverdueAmount, SeverityCode, StatusCode, ResolvedDate, Notes, AssignedToUserId, CreatedDateUtc FROM Billing.DelinquencyFlag WHERE DelinquencyFlagId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DelinquencyFlagDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DelinquencyFlagDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Billing.DelinquencyFlag", "DelinquencyFlagId, TenantId, AccountId, InvoiceId, FlagDate, DaysOverdue, OverdueAmount, SeverityCode, StatusCode, ResolvedDate, Notes, AssignedToUserId, CreatedDateUtc", "Notes LIKE '%' + @SearchTerm + '%' OR SeverityCode LIKE '%' + @SearchTerm + '%'", "FlagDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DelinquencyFlagDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DelinquencyFlagDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
