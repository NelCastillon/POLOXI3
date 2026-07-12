using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.BillingConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BillingConfigRepository : IBillingConfigRepository
{
    private readonly ISqlConnectionFactory _cf;
    public BillingConfigRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = "BillingConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc";

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.BillingConfigItem', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.BillingConfigItem
    (
        BillingConfigItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BillingConfigItem PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Kind NVARCHAR(80) NOT NULL,
        Code NVARCHAR(80) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Category NVARCHAR(120) NULL,
        Description NVARCHAR(500) NULL,
        ConfigurationJson NVARCHAR(4000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_BillingConfigItem_IsActive DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_BillingConfigItem_SortOrder DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingConfigItem_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_BillingConfigItem_IsDeleted DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'Category') IS NULL ALTER TABLE Billing.BillingConfigItem ADD Category NVARCHAR(120) NULL;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'Description') IS NULL ALTER TABLE Billing.BillingConfigItem ADD Description NVARCHAR(500) NULL;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'ConfigurationJson') IS NULL ALTER TABLE Billing.BillingConfigItem ADD ConfigurationJson NVARCHAR(4000) NULL;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'IsActive') IS NULL ALTER TABLE Billing.BillingConfigItem ADD IsActive BIT NOT NULL CONSTRAINT DF_BillingConfigItem_IsActive_Legacy DEFAULT 1;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'SortOrder') IS NULL ALTER TABLE Billing.BillingConfigItem ADD SortOrder INT NOT NULL CONSTRAINT DF_BillingConfigItem_SortOrder_Legacy DEFAULT 0;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.BillingConfigItem ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingConfigItem_CreatedDateUtc_Legacy DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'CreatedByUserId') IS NULL ALTER TABLE Billing.BillingConfigItem ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.BillingConfigItem ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.BillingConfigItem ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.BillingConfigItem', N'IsDeleted') IS NULL ALTER TABLE Billing.BillingConfigItem ADD IsDeleted BIT NOT NULL CONSTRAINT DF_BillingConfigItem_IsDeleted_Legacy DEFAULT 0;
END;";

        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: ct));

        if (tenantId is null) return;

        const string seedSql = @"
DECLARE @Defaults TABLE (Kind NVARCHAR(80), Code NVARCHAR(80), Name NVARCHAR(200), Category NVARCHAR(120), Description NVARCHAR(500), ConfigurationJson NVARCHAR(4000), SortOrder INT);

INSERT INTO @Defaults (Kind, Code, Name, Category, Description, ConfigurationJson, SortOrder)
VALUES
(N'PaymentProvider', N'STRIPE', N'Stripe Payments', N'Gateway', N'Card and ACH payment gateway configuration for hosted payment and tokenized billing workflows.', N'{""providerType"":""Stripe"",""environment"":""Sandbox"",""methods"":[""Card"",""ACH""],""settlementMode"":""Daily Batch"",""supportsCard"":true,""supportsAch"":true,""autoRetry"":true}', 10),
(N'PaymentProvider', N'AUTHNET', N'Authorize.Net', N'Gateway', N'Authorize.Net gateway configuration for card processing, merchant credentials, and settlement controls.', N'{""providerType"":""Authorize.Net"",""environment"":""Sandbox"",""methods"":[""Card"",""ACH""],""settlementMode"":""Daily Batch"",""supportsCard"":true,""supportsAch"":true,""autoRetry"":true}', 20),
(N'PaymentProvider', N'BANK_ACH', N'Bank ACH Processor', N'Method', N'ACH processor configuration for agency bill drafts, batch settlement, and retry workflows.', N'{""providerType"":""ACH Processor"",""environment"":""Production"",""methods"":[""ACH""],""settlementMode"":""Daily Batch"",""supportsCard"":false,""supportsAch"":true,""autoRetry"":true}', 30),
(N'PaymentPlan', N'MONTHLY_INSTALLMENTS', N'Monthly Installments', N'Installment', N'Monthly payment plan with down payment, recurring schedule, autopay, and reminder configuration.', N'{""frequency"":""Monthly"",""installmentCount"":10,""minimumDownPaymentPercent"":20,""autopayDefault"":true,""eligibility"":""Active policies with approved billing account""}', 10),
(N'PaymentPlan', N'QUARTERLY_INSTALLMENTS', N'Quarterly Installments', N'Schedule', N'Quarterly payment schedule for larger premiums with receivable controls and due-date reminders.', N'{""frequency"":""Quarterly"",""installmentCount"":4,""minimumDownPaymentPercent"":25,""autopayDefault"":false,""eligibility"":""Approved commercial accounts""}', 20),
(N'PaymentPlan', N'AGENCY_BILL_AUTOPAY', N'Agency Bill Autopay', N'Eligibility', N'Autopay-first plan for agency bill receivables, retry policy, and exception review.', N'{""frequency"":""Monthly"",""installmentCount"":12,""minimumDownPaymentPercent"":0,""autopayDefault"":true,""eligibility"":""Accounts enrolled in autopay""}', 30);

INSERT INTO Billing.BillingConfigItem (BillingConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, d.Kind, d.Code, d.Name, d.Category, d.Description, d.ConfigurationJson, 1, d.SortOrder, SYSUTCDATETIME(), NULL, 0
FROM @Defaults d
WHERE NOT EXISTS
(
    SELECT 1
    FROM Billing.BillingConfigItem existing
    WHERE existing.TenantId = @TenantId
      AND existing.Kind = d.Kind
      AND existing.Code = d.Code
);
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: ct));
    }

    public async Task<BillingConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(ct: ct);
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<BillingConfigItemDto>(new CommandDefinition($"SELECT {Cols} FROM Billing.BillingConfigItem WHERE BillingConfigItemId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<BillingConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, ct);
        const string sql = @"
SELECT BillingConfigItemId, TenantId, Kind, Code, Name, Category, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc
FROM Billing.BillingConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%')
ORDER BY SortOrder ASC, Name ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Billing.BillingConfigItem
WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0
  AND (@SearchTerm='' OR Name LIKE '%'+@SearchTerm+'%' OR Code LIKE '%'+@SearchTerm+'%' OR Category LIKE '%'+@SearchTerm+'%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        return new() { Items = (await multi.ReadAsync<BillingConfigItemDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateBillingConfigItemRequest r, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(r.TenantId, ct);
        const string sql = @"INSERT INTO Billing.BillingConfigItem (BillingConfigItemId,TenantId,Kind,Code,Name,Category,Description,ConfigurationJson,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@Kind,@Code,@Name,@Category,@Description,@ConfigurationJson,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.Kind, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateBillingConfigItemRequest r, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(ct: ct);
        const string sql = @"UPDATE Billing.BillingConfigItem SET Code=@Code,Name=@Name,Category=@Category,Description=@Description,ConfigurationJson=@ConfigurationJson,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE BillingConfigItemId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAndSeedAsync(ct: ct);
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Billing.BillingConfigItem SET IsDeleted=1 WHERE BillingConfigItemId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
