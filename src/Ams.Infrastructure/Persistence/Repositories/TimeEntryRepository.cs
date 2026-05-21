using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TimeEntryRepository : ITimeEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public TimeEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.TimeEntry', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.TimeEntry
    (
        TimeEntryId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        EngagementId    UNIQUEIDENTIFIER NULL,
        AccountId       UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        EntryDate       DATE             NOT NULL,
        Hours           DECIMAL(9, 2)    NOT NULL,
        BillableHours   DECIMAL(9, 2)    NOT NULL,
        RateAmount      DECIMAL(18, 2)   NOT NULL,
        Description     NVARCHAR(1000)   NULL,
        StatusCode      NVARCHAR(50)     NOT NULL DEFAULT N'Draft',
        InvoiceId       UNIQUEIDENTIFIER NULL,
        CreatedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2        NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted       BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Billing.TimeEntry', N'TenantId') IS NULL ALTER TABLE Billing.TimeEntry ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TimeEntry_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Billing.TimeEntry', N'EngagementId') IS NULL ALTER TABLE Billing.TimeEntry ADD EngagementId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.TimeEntry', N'AccountId') IS NULL ALTER TABLE Billing.TimeEntry ADD AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TimeEntry_AccountId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.TimeEntry', N'UserId') IS NULL ALTER TABLE Billing.TimeEntry ADD UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TimeEntry_UserId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.TimeEntry', N'EntryDate') IS NULL ALTER TABLE Billing.TimeEntry ADD EntryDate DATE NOT NULL CONSTRAINT DF_TimeEntry_EntryDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Billing.TimeEntry', N'Hours') IS NULL ALTER TABLE Billing.TimeEntry ADD Hours DECIMAL(9, 2) NOT NULL CONSTRAINT DF_TimeEntry_Hours DEFAULT 0;
    IF COL_LENGTH(N'Billing.TimeEntry', N'BillableHours') IS NULL ALTER TABLE Billing.TimeEntry ADD BillableHours DECIMAL(9, 2) NOT NULL CONSTRAINT DF_TimeEntry_BillableHours DEFAULT 0;
    IF COL_LENGTH(N'Billing.TimeEntry', N'RateAmount') IS NULL ALTER TABLE Billing.TimeEntry ADD RateAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_TimeEntry_RateAmount DEFAULT 0;
    IF COL_LENGTH(N'Billing.TimeEntry', N'Description') IS NULL ALTER TABLE Billing.TimeEntry ADD Description NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Billing.TimeEntry', N'StatusCode') IS NULL ALTER TABLE Billing.TimeEntry ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_TimeEntry_StatusCode DEFAULT N'Draft';
    IF COL_LENGTH(N'Billing.TimeEntry', N'InvoiceId') IS NULL ALTER TABLE Billing.TimeEntry ADD InvoiceId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.TimeEntry', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.TimeEntry ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_TimeEntry_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.TimeEntry', N'CreatedByUserId') IS NULL ALTER TABLE Billing.TimeEntry ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.TimeEntry', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.TimeEntry ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.TimeEntry', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.TimeEntry ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.TimeEntry', N'IsDeleted') IS NULL ALTER TABLE Billing.TimeEntry ADD IsDeleted BIT NOT NULL CONSTRAINT DF_TimeEntry_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.TimeEntry') AND name = N'IX_TimeEntry_Tenant_Date')
    CREATE INDEX IX_TimeEntry_Tenant_Date ON Billing.TimeEntry(TenantId, EntryDate DESC, IsDeleted);

IF OBJECT_ID(N'Billing.Timesheet', N'U') IS NOT NULL
   AND COL_LENGTH(N'Billing.Timesheet', N'StatusCodeId') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.default_constraints dc
       INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
       WHERE dc.parent_object_id = OBJECT_ID(N'Billing.Timesheet') AND c.name = N'StatusCodeId')
    ALTER TABLE Billing.Timesheet ADD CONSTRAINT DF_Timesheet_StatusCodeId DEFAULT 1 FOR StatusCodeId;

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Billing.TimeEntry WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    DECLARE @AccountId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
    DECLARE @UserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
    DECLARE @TimesheetId UNIQUEIDENTIFIER = NULL;

    IF OBJECT_ID(N'Billing.BillingAccount', N'U') IS NOT NULL
        SELECT TOP 1 @AccountId = AccountId FROM Billing.BillingAccount WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;

    IF OBJECT_ID(N'Client.Account', N'U') IS NOT NULL
        SELECT TOP 1 @AccountId = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;

    IF OBJECT_ID(N'Client.Account', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccountId)
        SELECT TOP 1 @AccountId = AccountId FROM Client.Account ORDER BY CreatedDateUtc DESC;

    IF OBJECT_ID(N'Identity.User', N'U') IS NOT NULL
        SELECT TOP 1 @UserId = UserId FROM [Identity].[User] WHERE TenantId = @TenantId ORDER BY CreatedDateUtc DESC;

    IF OBJECT_ID(N'IAM.User', N'U') IS NOT NULL
        SELECT TOP 1 @UserId = UserId FROM IAM.[User] WHERE TenantId = @TenantId ORDER BY CreatedDateUtc DESC;

    IF OBJECT_ID(N'IAM.User', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM IAM.[User] WHERE UserId = @UserId)
        SELECT TOP 1 @UserId = UserId FROM IAM.[User] ORDER BY CreatedDateUtc DESC;

    IF COL_LENGTH(N'Billing.TimeEntry', N'TimesheetId') IS NOT NULL AND OBJECT_ID(N'Billing.Timesheet', N'U') IS NOT NULL
    BEGIN
        SELECT TOP 1 @TimesheetId = TimesheetId FROM Billing.Timesheet WHERE TenantId = @TenantId ORDER BY CreatedDateUtc DESC;

        IF @TimesheetId IS NULL
        BEGIN
            SET @TimesheetId = NEWID();
            DECLARE @Columns NVARCHAR(MAX) = N'TimesheetId';
            DECLARE @Values NVARCHAR(MAX) = N'@TimesheetId';

            IF COL_LENGTH(N'Billing.Timesheet', N'TenantId') IS NOT NULL BEGIN SET @Columns += N', TenantId'; SET @Values += N', @TenantId'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'UserId') IS NOT NULL BEGIN SET @Columns += N', UserId'; SET @Values += N', @UserId'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'PeriodStartDate') IS NOT NULL BEGIN SET @Columns += N', PeriodStartDate'; SET @Values += N', DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME()))'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'PeriodEndDate') IS NOT NULL BEGIN SET @Columns += N', PeriodEndDate'; SET @Values += N', CONVERT(date, SYSUTCDATETIME())'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'PeriodStart') IS NOT NULL BEGIN SET @Columns += N', PeriodStart'; SET @Values += N', DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME()))'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'PeriodEnd') IS NOT NULL BEGIN SET @Columns += N', PeriodEnd'; SET @Values += N', CONVERT(date, SYSUTCDATETIME())'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'StartDate') IS NOT NULL BEGIN SET @Columns += N', StartDate'; SET @Values += N', DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME()))'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'EndDate') IS NOT NULL BEGIN SET @Columns += N', EndDate'; SET @Values += N', CONVERT(date, SYSUTCDATETIME())'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'StatusCode') IS NOT NULL BEGIN SET @Columns += N', StatusCode'; SET @Values += N', N''Approved'''; END
            IF COL_LENGTH(N'Billing.Timesheet', N'StatusCodeId') IS NOT NULL BEGIN SET @Columns += N', StatusCodeId'; SET @Values += N', 1'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'TotalHours') IS NOT NULL BEGIN SET @Columns += N', TotalHours'; SET @Values += N', 7.25'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'TotalBillableHours') IS NOT NULL BEGIN SET @Columns += N', TotalBillableHours'; SET @Values += N', 4.25'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'CreatedDateUtc') IS NOT NULL BEGIN SET @Columns += N', CreatedDateUtc'; SET @Values += N', SYSUTCDATETIME()'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'CreatedByUserId') IS NOT NULL BEGIN SET @Columns += N', CreatedByUserId'; SET @Values += N', @UserId'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'ModifiedDateUtc') IS NOT NULL BEGIN SET @Columns += N', ModifiedDateUtc'; SET @Values += N', NULL'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'ModifiedByUserId') IS NOT NULL BEGIN SET @Columns += N', ModifiedByUserId'; SET @Values += N', NULL'; END
            IF COL_LENGTH(N'Billing.Timesheet', N'IsDeleted') IS NOT NULL BEGIN SET @Columns += N', IsDeleted'; SET @Values += N', 0'; END

            DECLARE @TimesheetSql NVARCHAR(MAX) = N'INSERT INTO Billing.Timesheet (' + @Columns + N') VALUES (' + @Values + N');';
            EXEC sp_executesql @TimesheetSql, N'@TimesheetId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER', @TimesheetId, @TenantId, @UserId;

            IF NOT EXISTS (SELECT 1 FROM Billing.Timesheet WHERE TimesheetId = @TimesheetId)
                SET @TimesheetId = NULL;
        END
    END

    IF COL_LENGTH(N'Billing.TimeEntry', N'TimesheetId') IS NOT NULL AND @TimesheetId IS NOT NULL
    BEGIN
        INSERT INTO Billing.TimeEntry (TimeEntryId, TimesheetId, TenantId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES
            (NEWID(), @TimesheetId, @TenantId, @AccountId, @UserId, DATEADD(day, -1, CONVERT(date, SYSUTCDATETIME())), 2.50, 2.50, 175.00, N'Tenant Admin seeded client service review time entry.', N'Submitted', SYSUTCDATETIME(), 0),
            (NEWID(), @TimesheetId, @TenantId, @AccountId, @UserId, DATEADD(day, -3, CONVERT(date, SYSUTCDATETIME())), 1.75, 1.75, 175.00, N'Tenant Admin seeded billing workflow approval entry.', N'Approved', SYSUTCDATETIME(), 0),
            (NEWID(), @TimesheetId, @TenantId, @AccountId, @UserId, DATEADD(day, -6, CONVERT(date, SYSUTCDATETIME())), 3.00, 0.00, 0.00, N'Tenant Admin seeded internal operations time entry.', N'Draft', SYSUTCDATETIME(), 0);
    END
    ELSE IF COL_LENGTH(N'Billing.TimeEntry', N'TimesheetId') IS NULL
    BEGIN
        INSERT INTO Billing.TimeEntry (TimeEntryId, TenantId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES
            (NEWID(), @TenantId, @AccountId, @UserId, DATEADD(day, -1, CONVERT(date, SYSUTCDATETIME())), 2.50, 2.50, 175.00, N'Tenant Admin seeded client service review time entry.', N'Submitted', SYSUTCDATETIME(), 0),
            (NEWID(), @TenantId, @AccountId, @UserId, DATEADD(day, -3, CONVERT(date, SYSUTCDATETIME())), 1.75, 1.75, 175.00, N'Tenant Admin seeded billing workflow approval entry.', N'Approved', SYSUTCDATETIME(), 0),
            (NEWID(), @TenantId, @AccountId, @UserId, DATEADD(day, -6, CONVERT(date, SYSUTCDATETIME())), 3.00, 0.00, 0.00, N'Tenant Admin seeded internal operations time entry.', N'Draft', SYSUTCDATETIME(), 0);
    END
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<TimeEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "SELECT TimeEntryId, TenantId, EngagementId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, InvoiceId, CreatedDateUtc FROM Billing.TimeEntry WHERE TimeEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TimeEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TimeEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Billing.TimeEntry", "TimeEntryId, TenantId, EngagementId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, InvoiceId, CreatedDateUtc", "Description LIKE '%' + @SearchTerm + '%'", "EntryDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TimeEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TimeEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Billing.TimeEntry (TimeEntryId, TenantId, EngagementId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, InvoiceId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @EngagementId, @AccountId, @UserId, @EntryDate, @Hours, @BillableHours, @RateAmount, @Description, @StatusCode, @InvoiceId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.EngagementId, request.AccountId, request.UserId, request.EntryDate, request.Hours, request.BillableHours, request.RateAmount, request.Description, request.StatusCode, request.InvoiceId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
UPDATE Billing.TimeEntry
SET EngagementId = @EngagementId,
    AccountId = @AccountId,
    UserId = @UserId,
    EntryDate = @EntryDate,
    Hours = @Hours,
    BillableHours = @BillableHours,
    RateAmount = @RateAmount,
    Description = @Description,
    StatusCode = @StatusCode,
    InvoiceId = @InvoiceId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TimeEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EngagementId, request.AccountId, request.UserId, request.EntryDate, request.Hours, request.BillableHours, request.RateAmount, request.Description, request.StatusCode, request.InvoiceId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "UPDATE Billing.TimeEntry SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE TimeEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
