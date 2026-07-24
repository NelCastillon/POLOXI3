using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.BillingAccounts;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BillingAccountRepository : IBillingAccountRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BillingAccountRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureSchemaAndSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.AccountSettings', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.AccountSettings
    (
        AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BillingAccountSettings PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        BillingModeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAccountSettings_Mode DEFAULT N'Direct Bill',
        PaymentTermsCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAccountSettings_Terms DEFAULT N'Net 30',
        DefaultPaymentMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAccountSettings_Method DEFAULT N'ACH',
        CreditLimit DECIMAL(18,2) NOT NULL CONSTRAINT DF_BillingAccountSettings_CreditLimit DEFAULT 0,
        AutopayEnrolled BIT NOT NULL CONSTRAINT DF_BillingAccountSettings_Autopay DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingAccountSettings_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_BillingAccountSettings_IsDeleted DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Billing.AccountSettings', N'TenantId') IS NULL ALTER TABLE Billing.AccountSettings ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BillingAccountSettings_TenantId_Manual DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Billing.AccountSettings', N'BillingModeCode') IS NULL ALTER TABLE Billing.AccountSettings ADD BillingModeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAccountSettings_Mode_Manual DEFAULT N'Direct Bill';
    IF COL_LENGTH(N'Billing.AccountSettings', N'PaymentTermsCode') IS NULL ALTER TABLE Billing.AccountSettings ADD PaymentTermsCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAccountSettings_Terms_Manual DEFAULT N'Net 30';
    IF COL_LENGTH(N'Billing.AccountSettings', N'DefaultPaymentMethodCode') IS NULL ALTER TABLE Billing.AccountSettings ADD DefaultPaymentMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAccountSettings_Method_Manual DEFAULT N'ACH';
    IF COL_LENGTH(N'Billing.AccountSettings', N'CreditLimit') IS NULL ALTER TABLE Billing.AccountSettings ADD CreditLimit DECIMAL(18,2) NOT NULL CONSTRAINT DF_BillingAccountSettings_CreditLimit_Manual DEFAULT 0;
    IF COL_LENGTH(N'Billing.AccountSettings', N'AutopayEnrolled') IS NULL ALTER TABLE Billing.AccountSettings ADD AutopayEnrolled BIT NOT NULL CONSTRAINT DF_BillingAccountSettings_Autopay_Manual DEFAULT 0;
    IF COL_LENGTH(N'Billing.AccountSettings', N'CreatedDateUtc') IS NULL ALTER TABLE Billing.AccountSettings ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingAccountSettings_Created_Manual DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Billing.AccountSettings', N'CreatedByUserId') IS NULL ALTER TABLE Billing.AccountSettings ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.AccountSettings', N'ModifiedDateUtc') IS NULL ALTER TABLE Billing.AccountSettings ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Billing.AccountSettings', N'ModifiedByUserId') IS NULL ALTER TABLE Billing.AccountSettings ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Billing.AccountSettings', N'IsDeleted') IS NULL ALTER TABLE Billing.AccountSettings ADD IsDeleted BIT NOT NULL CONSTRAINT DF_BillingAccountSettings_IsDeleted_Manual DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Billing.AccountSettings') AND name = N'IX_BillingAccountSettings_Tenant')
    CREATE INDEX IX_BillingAccountSettings_Tenant ON Billing.AccountSettings(TenantId, IsDeleted, BillingModeCode, AutopayEnrolled);

";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<BillingAccountDto?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BillingAccountDto>(new CommandDefinition(SearchSql + " WHERE a.AccountId = @AccountId AND a.IsDeleted = 0 ORDER BY a.AccountName;", new { AccountId = accountId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BillingAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var where = @"WHERE a.TenantId = @TenantId AND a.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR a.AccountNumber LIKE '%' + @SearchTerm + '%' OR a.MainEmail LIKE '%' + @SearchTerm + '%')";
        var sql = $@"
{SearchSql}
{where}
ORDER BY a.AccountName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM Client.Account a
WHERE a.TenantId = @TenantId AND a.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR a.AccountNumber LIKE '%' + @SearchTerm + '%' OR a.MainEmail LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<BillingAccountDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BillingAccountDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<BillingModeDashboardRowDto>> GetBillingModeDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        const string sql = @"
SELECT a.AccountId,
       a.TenantId,
       a.AccountNumber,
       a.AccountName,
       COALESCE(s.BillingModeCode, N'Not Configured') AS BillingModeCode,
       COALESCE(inv.BilledAmount, 0) AS BilledAmount,
       COALESCE(inv.OutstandingAmount, 0) AS OutstandingAmount,
       COALESCE(s.CreditLimit, 0) AS CreditLimit,
       COALESCE(s.AutopayEnrolled, 0) AS AutopayEnrolled,
       a.StatusCode,
       CASE
            WHEN s.BillingModeCode = N'Agency Bill' THEN inv.NextOpenDueDate
           ELSE NULL
       END AS NextRemittanceDueDate
FROM Client.Account a
LEFT JOIN Billing.AccountSettings s ON s.AccountId = a.AccountId AND s.IsDeleted = 0
OUTER APPLY
(
    SELECT SUM(i.TotalAmount) AS BilledAmount,
           SUM(i.BalanceAmount) AS OutstandingAmount,
           MIN(CASE WHEN i.BalanceAmount > 0 THEN CAST(i.DueDate AS DATETIME2) END) AS NextOpenDueDate
    FROM Billing.Invoice i
    WHERE i.AccountId = a.AccountId
      AND i.TenantId = a.TenantId
      AND i.IsDeleted = 0
) inv
WHERE a.TenantId = @TenantId
  AND a.IsDeleted = 0
ORDER BY a.AccountName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<BillingModeDashboardRowDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateAsync(CreateBillingAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AccountId == Guid.Empty) throw new InvalidOperationException("Account is required.");
        const string sql = @"
IF EXISTS (SELECT 1 FROM Billing.AccountSettings WHERE AccountId=@AccountId)
BEGIN
    UPDATE Billing.AccountSettings
    SET BillingModeCode=@BillingModeCode, PaymentTermsCode=@PaymentTermsCode, DefaultPaymentMethodCode=@DefaultPaymentMethodCode,
        CreditLimit=@CreditLimit, AutopayEnrolled=@AutopayEnrolled, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@CreatedByUserId, IsDeleted=0
    WHERE AccountId=@AccountId;
END
ELSE
BEGIN
    INSERT INTO Billing.AccountSettings (AccountId,TenantId,BillingModeCode,PaymentTermsCode,DefaultPaymentMethodCode,CreditLimit,AutopayEnrolled,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES (@AccountId,@TenantId,@BillingModeCode,@PaymentTermsCode,@DefaultPaymentMethodCode,@CreditLimit,@AutopayEnrolled,SYSUTCDATETIME(),@CreatedByUserId,0);
END
UPDATE Client.Account SET StatusCode=@StatusCode, ModifiedDateUtc=SYSUTCDATETIME() WHERE AccountId=@AccountId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
        return request.AccountId;
    }

    public async Task UpdateAsync(Guid accountId, UpdateBillingAccountRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Billing.AccountSettings
SET BillingModeCode=@BillingModeCode, PaymentTermsCode=@PaymentTermsCode, DefaultPaymentMethodCode=@DefaultPaymentMethodCode,
    CreditLimit=@CreditLimit, AutopayEnrolled=@AutopayEnrolled, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@ModifiedByUserId, IsDeleted=0
WHERE AccountId=@AccountId;
UPDATE Client.Account SET StatusCode=@StatusCode, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@ModifiedByUserId WHERE AccountId=@AccountId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AccountId = accountId, request.BillingModeCode, request.PaymentTermsCode, request.DefaultPaymentMethodCode, request.CreditLimit, request.AutopayEnrolled, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid accountId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Billing.AccountSettings SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@ModifiedByUserId WHERE AccountId=@AccountId;
UPDATE Client.Account SET StatusCode=N'Closed', ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@ModifiedByUserId WHERE AccountId=@AccountId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AccountId = accountId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    private const string SearchSql = @"
SELECT a.AccountId, a.TenantId, a.AccountNumber, a.AccountName,
       COALESCE(s.BillingModeCode, N'Direct Bill') AS BillingModeCode,
       COALESCE(inv.BalanceAmount, 0) AS BalanceAmount,
       COALESCE(s.CreditLimit, 0) AS CreditLimit,
       COALESCE(s.PaymentTermsCode, N'Net 30') AS PaymentTermsCode,
       COALESCE(s.DefaultPaymentMethodCode, N'ACH') AS DefaultPaymentMethodCode,
       COALESCE(s.AutopayEnrolled, 0) AS AutopayEnrolled,
       a.StatusCode,
       0 AS PolicyCount,
       pay.LastPaymentDate,
       a.MainEmail,
       a.MainPhone
FROM Client.Account a
LEFT JOIN Billing.AccountSettings s ON s.AccountId=a.AccountId AND s.IsDeleted=0
OUTER APPLY (SELECT SUM(BalanceAmount) AS BalanceAmount FROM Billing.Invoice i WHERE i.AccountId=a.AccountId AND i.IsDeleted=0) inv
OUTER APPLY (SELECT MAX(CAST(PaymentDate AS DATETIME2)) AS LastPaymentDate FROM Billing.Payment p WHERE p.AccountId=a.AccountId AND p.IsDeleted=0) pay";
}
