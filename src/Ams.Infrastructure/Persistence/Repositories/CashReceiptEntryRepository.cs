using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CashReceiptEntryRepository : ICashReceiptEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CashReceiptEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance') EXEC(N'CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.CashReceiptEntry', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.CashReceiptEntry
    (
        CashReceiptEntryId uniqueidentifier NOT NULL CONSTRAINT PK_CashReceiptEntry PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        AccountId uniqueidentifier NOT NULL,
        InvoiceId uniqueidentifier NULL,
        ReceiptDate date NOT NULL,
        Amount decimal(18,2) NOT NULL CONSTRAINT DF_CashReceiptEntry_Amount DEFAULT (0),
        PaymentMethodCode nvarchar(50) NOT NULL CONSTRAINT DF_CashReceiptEntry_Method DEFAULT ('ACH'),
        ReferenceNumber nvarchar(100) NULL,
        GLAccountId uniqueidentifier NULL,
        BankAccountCode nvarchar(80) NULL,
        Notes nvarchar(1000) NULL,
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_CashReceiptEntry_Status DEFAULT ('Pending'),
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_CashReceiptEntry_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_CashReceiptEntry_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH(N'Finance.CashReceiptEntry', N'TenantId') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'AccountId') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD AccountId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'InvoiceId') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD InvoiceId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'ReceiptDate') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD ReceiptDate date NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'Amount') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD Amount decimal(18,2) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'PaymentMethodCode') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD PaymentMethodCode nvarchar(50) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'ReferenceNumber') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD ReferenceNumber nvarchar(100) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'GLAccountId') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD GLAccountId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'BankAccountCode') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD BankAccountCode nvarchar(80) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'Notes') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'StatusCode') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'CreatedByUserId') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'ModifiedDateUtc') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'ModifiedByUserId') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH(N'Finance.CashReceiptEntry', N'IsDeleted') IS NULL ALTER TABLE Finance.CashReceiptEntry ADD IsDeleted bit NULL;

UPDATE Finance.CashReceiptEntry SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.CashReceiptEntry SET ReceiptDate = COALESCE(ReceiptDate, CAST(SYSUTCDATETIME() AS date)) WHERE ReceiptDate IS NULL;
UPDATE Finance.CashReceiptEntry SET Amount = COALESCE(Amount, 0) WHERE Amount IS NULL;
UPDATE Finance.CashReceiptEntry SET PaymentMethodCode = COALESCE(NULLIF(PaymentMethodCode, ''), 'ACH') WHERE PaymentMethodCode IS NULL OR PaymentMethodCode = '';
UPDATE Finance.CashReceiptEntry SET StatusCode = COALESCE(NULLIF(StatusCode, ''), 'Pending') WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.CashReceiptEntry SET ReferenceNumber = COALESCE(NULLIF(ReferenceNumber, ''), CONCAT('CR-', FORMAT(CreatedDateUtc, 'yyyyMMdd'), '-', LEFT(CONVERT(nvarchar(36), CashReceiptEntryId), 8))) WHERE ReferenceNumber IS NULL OR ReferenceNumber = '';
UPDATE Finance.CashReceiptEntry SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.CashReceiptEntry SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF OBJECT_ID(N'Client.Account', N'U') IS NOT NULL
BEGIN
    UPDATE cr
    SET AccountId = a.AccountId
    FROM Finance.CashReceiptEntry cr
    CROSS APPLY (
        SELECT TOP (1) AccountId
        FROM Client.Account a
        WHERE (@TenantId IS NULL OR a.TenantId = COALESCE(cr.TenantId, @TenantId))
          AND COALESCE(a.IsDeleted, 0) = 0
        ORDER BY a.CreatedDateUtc DESC
    ) a
    WHERE cr.AccountId IS NULL;
END;

IF OBJECT_ID(N'Finance.GLAccount', N'U') IS NOT NULL
BEGIN
    UPDATE cr
    SET GLAccountId = gl.GLAccountId
    FROM Finance.CashReceiptEntry cr
    CROSS APPLY (
        SELECT TOP (1) GLAccountId
        FROM Finance.GLAccount gl
        WHERE (@TenantId IS NULL OR gl.TenantId = COALESCE(cr.TenantId, @TenantId))
          AND COALESCE(gl.IsDeleted, 0) = 0
          AND (gl.AccountCode = '1000' OR gl.AccountName LIKE '%Cash%')
        ORDER BY gl.AccountCode
    ) gl
    WHERE cr.GLAccountId IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashReceiptEntry_Tenant_Date' AND object_id = OBJECT_ID(N'Finance.CashReceiptEntry'))
    CREATE INDEX IX_CashReceiptEntry_Tenant_Date ON Finance.CashReceiptEntry (TenantId, ReceiptDate DESC, StatusCode) INCLUDE (Amount, PaymentMethodCode, ReferenceNumber, AccountId);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF OBJECT_ID(N'Client.Account', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Finance.CashReceiptEntry WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    DECLARE @CashGL uniqueidentifier = (
        SELECT TOP (1) GLAccountId
        FROM Finance.GLAccount
        WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0 AND (AccountCode = '1000' OR AccountName LIKE '%Cash%')
        ORDER BY AccountCode
    );

    DECLARE @Seed TABLE (RowNum int identity(1,1), AccountId uniqueidentifier, InvoiceId uniqueidentifier NULL);
    INSERT INTO @Seed (AccountId, InvoiceId)
    SELECT TOP (6) a.AccountId, i.InvoiceId
    FROM Client.Account a
    OUTER APPLY (
        SELECT TOP (1) InvoiceId
        FROM Billing.Invoice i
        WHERE OBJECT_ID(N'Billing.Invoice', N'U') IS NOT NULL AND i.AccountId = a.AccountId AND COALESCE(i.IsDeleted, 0) = 0
        ORDER BY i.CreatedDateUtc DESC
    ) i
    WHERE a.TenantId = @TenantId AND COALESCE(a.IsDeleted, 0) = 0
    ORDER BY a.CreatedDateUtc DESC;

    INSERT INTO Finance.CashReceiptEntry (CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, AccountId, InvoiceId,
           DATEADD(day, -RowNum * 3, CAST(SYSUTCDATETIME() AS date)),
           CASE RowNum WHEN 1 THEN 2450 WHEN 2 THEN 3800 WHEN 3 THEN 1250 WHEN 4 THEN 7200 WHEN 5 THEN 950 ELSE 1640 END,
           CASE RowNum % 5 WHEN 0 THEN 'Wire' WHEN 1 THEN 'ACH' WHEN 2 THEN 'Check' WHEN 3 THEN 'Credit Card' ELSE 'Cash' END,
           CONCAT('CR-', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd'), '-', RIGHT(CONCAT('000', RowNum), 3)),
           @CashGL,
           CASE WHEN RowNum % 2 = 0 THEN 'OPERATING' ELSE 'LOCKBOX' END,
           'Seeded cash receipt synchronized for finance workflow validation.',
           CASE WHEN RowNum IN (1,2,4) THEN 'Posted' WHEN RowNum = 6 THEN 'Void' ELSE 'Pending' END,
           SYSUTCDATETIME(), NULL, NULL, NULL, 0
    FROM @Seed;
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<CashReceiptEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = "SELECT CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc FROM Finance.CashReceiptEntry WHERE CashReceiptEntryId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CashReceiptEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CashReceiptEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc";
        var searchPredicate = "ReferenceNumber LIKE '%' + @SearchTerm + '%' OR PaymentMethodCode LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%' OR BankAccountCode LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%'";
        var searchSql = RepositorySql.BuildPagedSearchSql("Finance.CashReceiptEntry", selectColumns, searchPredicate, "ReceiptDate DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CashReceiptEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CashReceiptEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCashReceiptEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.CashReceiptEntry (CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @InvoiceId, @ReceiptDate, @Amount, @PaymentMethodCode, @ReferenceNumber, @GLAccountId, @BankAccountCode, @Notes, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.InvoiceId, request.ReceiptDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.GLAccountId, request.BankAccountCode, request.Notes, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        await SyncPostedReceiptToBillingAsync(cn, id, request.TenantId, request.AccountId, request.InvoiceId, request.ReceiptDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.StatusCode, request.Notes, request.CreatedByUserId, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCashReceiptEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.CashReceiptEntry
SET AccountId = @AccountId,
    InvoiceId = @InvoiceId,
    ReceiptDate = @ReceiptDate,
    Amount = @Amount,
    PaymentMethodCode = @PaymentMethodCode,
    ReferenceNumber = @ReferenceNumber,
    GLAccountId = @GLAccountId,
    BankAccountCode = @BankAccountCode,
    Notes = @Notes,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CashReceiptEntryId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.InvoiceId, request.ReceiptDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.GLAccountId, request.BankAccountCode, request.Notes, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        await SyncPostedReceiptToBillingAsync(cn, id, request.TenantId, request.AccountId, request.InvoiceId, request.ReceiptDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.StatusCode, request.Notes, request.ModifiedByUserId, cancellationToken);
    }

    private static async Task SyncPostedReceiptToBillingAsync(System.Data.IDbConnection connection, Guid cashReceiptEntryId, Guid tenantId, Guid accountId, Guid? invoiceId, DateOnly receiptDate, decimal amount, string paymentMethodCode, string? referenceNumber, string statusCode, string? notes, Guid? userId, CancellationToken cancellationToken)
    {
        if (!string.Equals(statusCode, "Posted", StringComparison.OrdinalIgnoreCase) || amount <= 0)
        {
            return;
        }

        const string sql = @"
IF OBJECT_ID(N'Billing.Payment', N'U') IS NOT NULL
BEGIN
    DECLARE @HasPaymentNumber bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'PaymentNumber') IS NULL THEN 0 ELSE 1 END;
    DECLARE @HasInvoiceId bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'InvoiceId') IS NULL THEN 0 ELSE 1 END;
    DECLARE @HasCurrencyCode bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'CurrencyCode') IS NULL THEN 0 ELSE 1 END;
    DECLARE @HasTotalAmount bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'TotalAmount') IS NULL THEN 0 ELSE 1 END;
    DECLARE @HasPaymentStatusCodeId bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'PaymentStatusCodeId') IS NULL THEN 0 ELSE 1 END;
    DECLARE @HasStatusCode bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'StatusCode') IS NULL THEN 0 ELSE 1 END;
    DECLARE @HasNotes bit = CASE WHEN COL_LENGTH(N'Billing.Payment', N'Notes') IS NULL THEN 0 ELSE 1 END;
    DECLARE @PaymentId uniqueidentifier = NULL;
    DECLARE @PaymentNumber nvarchar(50) = CONCAT(N'CR-', LEFT(CONVERT(nvarchar(36), @CashReceiptEntryId), 8));

    IF @HasNotes = 1
    BEGIN
        SELECT TOP (1) @PaymentId = PaymentId
        FROM Billing.Payment
        WHERE TenantId = @TenantId
          AND COALESCE(IsDeleted, 0) = 0
          AND Notes LIKE CONCAT(N'%CashReceiptEntryId:', CONVERT(nvarchar(36), @CashReceiptEntryId), N'%');
    END;

    IF @PaymentId IS NULL AND NULLIF(@ReferenceNumber, N'') IS NOT NULL
    BEGIN
        SELECT TOP (1) @PaymentId = PaymentId
        FROM Billing.Payment
        WHERE TenantId = @TenantId
          AND COALESCE(IsDeleted, 0) = 0
          AND ReferenceNumber = @ReferenceNumber;
    END;

    IF @PaymentId IS NULL
    BEGIN
        SET @PaymentId = NEWID();
        DECLARE @Columns nvarchar(max) = N'PaymentId, TenantId, AccountId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, CreatedDateUtc, CreatedByUserId, IsDeleted';
        DECLARE @Values nvarchar(max) = N'@PaymentId, @TenantId, @AccountId, @PaymentDate, @Amount, @PaymentMethodCode, @ReferenceNumber, SYSUTCDATETIME(), @UserId, 0';

        IF @HasPaymentNumber = 1 BEGIN SET @Columns = REPLACE(@Columns, N'PaymentId,', N'PaymentId, PaymentNumber,'); SET @Values = REPLACE(@Values, N'@PaymentId,', N'@PaymentId, @PaymentNumber,'); END;
        IF @HasInvoiceId = 1 BEGIN SET @Columns = REPLACE(@Columns, N'AccountId,', N'AccountId, InvoiceId,'); SET @Values = REPLACE(@Values, N'@AccountId,', N'@AccountId, @InvoiceId,'); END;
        IF @HasCurrencyCode = 1 BEGIN SET @Columns = REPLACE(@Columns, N'Amount,', N'CurrencyCode, Amount,'); SET @Values = REPLACE(@Values, N'@Amount,', N'N''USD'', @Amount,'); END;
        IF @HasTotalAmount = 1 BEGIN SET @Columns = REPLACE(@Columns, N'Amount,', N'TotalAmount, Amount,'); SET @Values = REPLACE(@Values, N'@Amount,', N'@Amount, @Amount,'); END;
        IF @HasPaymentStatusCodeId = 1 BEGIN SET @Columns = REPLACE(@Columns, N'ReferenceNumber,', N'ReferenceNumber, PaymentStatusCodeId,'); SET @Values = REPLACE(@Values, N'@ReferenceNumber,', N'@ReferenceNumber, 2,'); END;
        IF @HasStatusCode = 1 BEGIN SET @Columns = REPLACE(@Columns, N'ReferenceNumber,', N'ReferenceNumber, StatusCode,'); SET @Values = REPLACE(@Values, N'@ReferenceNumber,', N'@ReferenceNumber, N''Applied'','); END;
        IF @HasNotes = 1 BEGIN SET @Columns = REPLACE(@Columns, N'CreatedDateUtc,', N'Notes, CreatedDateUtc,'); SET @Values = REPLACE(@Values, N'SYSUTCDATETIME(),', N'@BillingNotes, SYSUTCDATETIME(),'); END;

        DECLARE @InsertSql nvarchar(max) = CONCAT(N'INSERT INTO Billing.Payment (', @Columns, N') VALUES (', @Values, N');');
        EXEC sp_executesql @InsertSql, N'@PaymentId uniqueidentifier, @PaymentNumber nvarchar(50), @TenantId uniqueidentifier, @AccountId uniqueidentifier, @InvoiceId uniqueidentifier, @PaymentDate date, @Amount decimal(18,2), @PaymentMethodCode nvarchar(50), @ReferenceNumber nvarchar(100), @BillingNotes nvarchar(500), @UserId uniqueidentifier', @PaymentId, @PaymentNumber, @TenantId, @AccountId, @InvoiceId, @ReceiptDate, @Amount, @PaymentMethodCode, @ReferenceNumber, @BillingNotes, @UserId;
    END
    ELSE
    BEGIN
        UPDATE Billing.Payment
        SET AccountId = @AccountId,
            PaymentDate = @ReceiptDate,
            Amount = @Amount,
            PaymentMethodCode = @PaymentMethodCode,
            ReferenceNumber = @ReferenceNumber
        WHERE PaymentId = @PaymentId;

        IF @HasInvoiceId = 1 UPDATE Billing.Payment SET InvoiceId = @InvoiceId WHERE PaymentId = @PaymentId;
        IF @HasTotalAmount = 1 UPDATE Billing.Payment SET TotalAmount = @Amount WHERE PaymentId = @PaymentId;
        IF @HasPaymentStatusCodeId = 1 UPDATE Billing.Payment SET PaymentStatusCodeId = 2 WHERE PaymentId = @PaymentId;
        IF @HasStatusCode = 1 UPDATE Billing.Payment SET StatusCode = N'Applied' WHERE PaymentId = @PaymentId;
        IF @HasNotes = 1 UPDATE Billing.Payment SET Notes = @BillingNotes WHERE PaymentId = @PaymentId;
    END;
END;

IF @InvoiceId IS NOT NULL AND OBJECT_ID(N'Billing.Invoice', N'U') IS NOT NULL AND COL_LENGTH(N'Billing.Invoice', N'BalanceAmount') IS NOT NULL
BEGIN
    UPDATE Billing.Invoice
    SET BalanceAmount = CASE WHEN BalanceAmount - @Amount < 0 THEN 0 ELSE BalanceAmount - @Amount END
    WHERE InvoiceId = @InvoiceId
      AND TenantId = @TenantId
      AND COALESCE(IsDeleted, 0) = 0;

    IF COL_LENGTH(N'Billing.Invoice', N'InvoiceStatusCodeId') IS NOT NULL
    BEGIN
        UPDATE Billing.Invoice
        SET InvoiceStatusCodeId = CASE WHEN BalanceAmount <= 0 THEN 3 ELSE InvoiceStatusCodeId END
        WHERE InvoiceId = @InvoiceId
          AND TenantId = @TenantId
          AND COALESCE(IsDeleted, 0) = 0;
    END;
END;";

        var billingNotes = string.Join(' ', new[] { notes, $"CashReceiptEntryId:{cashReceiptEntryId}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
        await connection.ExecuteAsync(new CommandDefinition(sql, new { CashReceiptEntryId = cashReceiptEntryId, TenantId = tenantId, AccountId = accountId, InvoiceId = invoiceId, ReceiptDate = receiptDate, Amount = amount, PaymentMethodCode = paymentMethodCode, ReferenceNumber = referenceNumber, BillingNotes = billingNotes, UserId = userId }, cancellationToken: cancellationToken));
    }
}
