using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ExpenseRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.ExpenseEntry', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.ExpenseEntry
    (
        ExpenseId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        EngagementId    UNIQUEIDENTIFIER NULL,
        AccountId       UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        ExpenseDate     DATE             NOT NULL,
        CategoryCode    NVARCHAR(80)     NOT NULL,
        Amount          DECIMAL(18, 2)   NOT NULL,
        Description     NVARCHAR(1000)   NULL,
        IsBillable      BIT              NOT NULL DEFAULT 1,
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
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'TenantId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ExpenseEntry_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'EngagementId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD EngagementId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'AccountId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ExpenseEntry_AccountId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'UserId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ExpenseEntry_UserId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'ExpenseDate') IS NULL ALTER TABLE Billing.ExpenseEntry ADD ExpenseDate DATE NOT NULL CONSTRAINT DF_ExpenseEntry_ExpenseDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'CategoryCode') IS NULL ALTER TABLE Billing.ExpenseEntry ADD CategoryCode NVARCHAR(80) NOT NULL CONSTRAINT DF_ExpenseEntry_CategoryCode DEFAULT N'Other';
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'Amount') IS NULL ALTER TABLE Billing.ExpenseEntry ADD Amount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ExpenseEntry_Amount DEFAULT 0;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'Description') IS NULL ALTER TABLE Billing.ExpenseEntry ADD Description NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'IsBillable') IS NULL ALTER TABLE Billing.ExpenseEntry ADD IsBillable BIT NOT NULL CONSTRAINT DF_ExpenseEntry_IsBillable DEFAULT 1;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'StatusCode') IS NULL ALTER TABLE Billing.ExpenseEntry ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ExpenseEntry_StatusCode DEFAULT N'Draft';
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'InvoiceId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD InvoiceId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.ExpenseEntry ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ExpenseEntry_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'CreatedByUserId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.ExpenseEntry ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.ExpenseEntry ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.ExpenseEntry', N'IsDeleted') IS NULL ALTER TABLE Billing.ExpenseEntry ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ExpenseEntry_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.ExpenseEntry') AND name = N'IX_ExpenseEntry_Tenant_Date')
    CREATE INDEX IX_ExpenseEntry_Tenant_Date ON Billing.ExpenseEntry(TenantId, ExpenseDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Billing.ExpenseEntry WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    DECLARE @AccountId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 AccountId FROM Billing.BillingAccount WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC), '00000000-0000-0000-0000-000000000000');
    DECLARE @UserId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 UserId FROM [Identity].[User] WHERE TenantId = @TenantId ORDER BY CreatedDateUtc DESC), '00000000-0000-0000-0000-000000000000');

    INSERT INTO Billing.ExpenseEntry (ExpenseId, TenantId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @AccountId, @UserId, DATEADD(day, -2, CONVERT(date, SYSUTCDATETIME())), N'Travel', 245.75, N'Tenant Admin seeded client travel expense for billing workflow validation.', 1, N'Submitted', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, @AccountId, @UserId, DATEADD(day, -4, CONVERT(date, SYSUTCDATETIME())), N'Inspection', 510.00, N'Tenant Admin seeded inspection expense synchronized with billing.', 1, N'Approved', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, @AccountId, @UserId, DATEADD(day, -7, CONVERT(date, SYSUTCDATETIME())), N'Supplies', 84.25, N'Tenant Admin seeded internal office supplies expense.', 0, N'Draft', SYSUTCDATETIME(), 0);
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<ExpenseEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "SELECT ExpenseId, TenantId, EngagementId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, InvoiceId, CreatedDateUtc FROM Billing.ExpenseEntry WHERE ExpenseId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ExpenseEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ExpenseEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Billing.ExpenseEntry", "ExpenseId, TenantId, EngagementId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, InvoiceId, CreatedDateUtc", "Description LIKE '%' + @SearchTerm + '%' OR CategoryCode LIKE '%' + @SearchTerm + '%'", "ExpenseDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ExpenseEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ExpenseEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Billing.ExpenseEntry (ExpenseId, TenantId, EngagementId, AccountId, UserId, ExpenseDate, CategoryCode, Amount, Description, IsBillable, StatusCode, InvoiceId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @EngagementId, @AccountId, @UserId, @ExpenseDate, @CategoryCode, @Amount, @Description, @IsBillable, @StatusCode, @InvoiceId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.EngagementId, request.AccountId, request.UserId, request.ExpenseDate, request.CategoryCode, request.Amount, request.Description, request.IsBillable, request.StatusCode, request.InvoiceId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
UPDATE Billing.ExpenseEntry
SET EngagementId = @EngagementId,
    AccountId = @AccountId,
    UserId = @UserId,
    ExpenseDate = @ExpenseDate,
    CategoryCode = @CategoryCode,
    Amount = @Amount,
    Description = @Description,
    IsBillable = @IsBillable,
    StatusCode = @StatusCode,
    InvoiceId = @InvoiceId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ExpenseId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EngagementId, request.AccountId, request.UserId, request.ExpenseDate, request.CategoryCode, request.Amount, request.Description, request.IsBillable, request.StatusCode, request.InvoiceId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "UPDATE Billing.ExpenseEntry SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE ExpenseId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
