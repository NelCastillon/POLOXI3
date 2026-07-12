using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ApPaymentRepository : IApPaymentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ApPaymentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Finance')
    EXEC('CREATE SCHEMA Finance');

IF OBJECT_ID('Finance.Vendor', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.Vendor
    (
        VendorId uniqueidentifier NOT NULL CONSTRAINT PK_Vendor PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        VendorCode nvarchar(50) NOT NULL,
        VendorName nvarchar(200) NOT NULL,
        ContactName nvarchar(150) NULL,
        Email nvarchar(254) NULL,
        Phone nvarchar(50) NULL,
        PaymentTermsCode nvarchar(50) NOT NULL CONSTRAINT DF_Vendor_PaymentTermsCode DEFAULT ('Net30'),
        CurrencyCode nvarchar(3) NOT NULL CONSTRAINT DF_Vendor_CurrencyCode DEFAULT ('USD'),
        TaxId nvarchar(50) NULL,
        VendorTypeCode nvarchar(80) NOT NULL CONSTRAINT DF_Vendor_VendorTypeCode DEFAULT ('Supplier'),
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_Vendor_StatusCode DEFAULT ('Active'),
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_Vendor_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_Vendor_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH('Finance.Vendor', 'TenantId') IS NULL ALTER TABLE Finance.Vendor ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.Vendor', 'VendorCode') IS NULL ALTER TABLE Finance.Vendor ADD VendorCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'VendorName') IS NULL ALTER TABLE Finance.Vendor ADD VendorName nvarchar(200) NULL;
IF COL_LENGTH('Finance.Vendor', 'PaymentTermsCode') IS NULL ALTER TABLE Finance.Vendor ADD PaymentTermsCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'CurrencyCode') IS NULL ALTER TABLE Finance.Vendor ADD CurrencyCode nvarchar(3) NULL;
IF COL_LENGTH('Finance.Vendor', 'VendorTypeCode') IS NULL ALTER TABLE Finance.Vendor ADD VendorTypeCode nvarchar(80) NULL;
IF COL_LENGTH('Finance.Vendor', 'StatusCode') IS NULL ALTER TABLE Finance.Vendor ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.Vendor ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.Vendor', 'IsDeleted') IS NULL ALTER TABLE Finance.Vendor ADD IsDeleted bit NULL;
UPDATE Finance.Vendor SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.Vendor SET VendorCode = COALESCE(NULLIF(VendorCode, ''), CONCAT('VEN-', RIGHT(CONVERT(varchar(36), VendorId), 6))) WHERE VendorCode IS NULL OR VendorCode = '';
UPDATE Finance.Vendor SET VendorName = COALESCE(NULLIF(VendorName, ''), 'Vendor Account') WHERE VendorName IS NULL OR VendorName = '';
UPDATE Finance.Vendor SET PaymentTermsCode = COALESCE(NULLIF(PaymentTermsCode, ''), 'Net30') WHERE PaymentTermsCode IS NULL OR PaymentTermsCode = '';
UPDATE Finance.Vendor SET CurrencyCode = COALESCE(NULLIF(CurrencyCode, ''), 'USD') WHERE CurrencyCode IS NULL OR CurrencyCode = '';
UPDATE Finance.Vendor SET VendorTypeCode = COALESCE(NULLIF(VendorTypeCode, ''), 'Supplier') WHERE VendorTypeCode IS NULL OR VendorTypeCode = '';
UPDATE Finance.Vendor SET StatusCode = COALESCE(NULLIF(StatusCode, ''), 'Active') WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.Vendor SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.Vendor SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF OBJECT_ID('Finance.ApInvoice', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.ApInvoice
    (
        ApInvoiceId uniqueidentifier NOT NULL CONSTRAINT PK_ApInvoice PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        VendorId uniqueidentifier NOT NULL,
        InvoiceNumber nvarchar(80) NOT NULL,
        InvoiceDate date NOT NULL,
        DueDate date NOT NULL,
        Amount decimal(18,2) NOT NULL CONSTRAINT DF_ApInvoice_Amount DEFAULT (0),
        AmountPaid decimal(18,2) NOT NULL CONSTRAINT DF_ApInvoice_AmountPaid DEFAULT (0),
        TaxAmount decimal(18,2) NOT NULL CONSTRAINT DF_ApInvoice_TaxAmount DEFAULT (0),
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_ApInvoice_StatusCode DEFAULT ('Open'),
        GLAccountId uniqueidentifier NULL,
        AgreementId uniqueidentifier NULL,
        Description nvarchar(1000) NULL,
        Notes nvarchar(1000) NULL,
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_ApInvoice_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ApInvoice_IsDeleted DEFAULT (0)
    );
END;

IF OBJECT_ID('Finance.ApPayment', 'U') IS NULL
BEGIN
    CREATE TABLE Finance.ApPayment
    (
        ApPaymentId uniqueidentifier NOT NULL CONSTRAINT PK_ApPayment PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        VendorId uniqueidentifier NOT NULL,
        ApInvoiceId uniqueidentifier NULL,
        PaymentDate date NOT NULL,
        Amount decimal(18,2) NOT NULL CONSTRAINT DF_ApPayment_Amount DEFAULT (0),
        PaymentMethodCode nvarchar(50) NOT NULL CONSTRAINT DF_ApPayment_Method DEFAULT ('ACH'),
        ReferenceNumber nvarchar(100) NULL,
        Notes nvarchar(1000) NULL,
        StatusCode nvarchar(50) NOT NULL CONSTRAINT DF_ApPayment_Status DEFAULT ('Pending'),
        CreatedDateUtc datetime2(7) NOT NULL CONSTRAINT DF_ApPayment_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId uniqueidentifier NULL,
        ModifiedDateUtc datetime2(7) NULL,
        ModifiedByUserId uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ApPayment_IsDeleted DEFAULT (0)
    );
END;

IF COL_LENGTH('Finance.ApPayment', 'TenantId') IS NULL ALTER TABLE Finance.ApPayment ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApPayment', 'VendorId') IS NULL ALTER TABLE Finance.ApPayment ADD VendorId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApPayment', 'ApInvoiceId') IS NULL ALTER TABLE Finance.ApPayment ADD ApInvoiceId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApPayment', 'PaymentDate') IS NULL ALTER TABLE Finance.ApPayment ADD PaymentDate date NULL;
IF COL_LENGTH('Finance.ApPayment', 'Amount') IS NULL ALTER TABLE Finance.ApPayment ADD Amount decimal(18,2) NULL;
IF COL_LENGTH('Finance.ApPayment', 'PaymentMethodCode') IS NULL ALTER TABLE Finance.ApPayment ADD PaymentMethodCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.ApPayment', 'ReferenceNumber') IS NULL ALTER TABLE Finance.ApPayment ADD ReferenceNumber nvarchar(100) NULL;
IF COL_LENGTH('Finance.ApPayment', 'Notes') IS NULL ALTER TABLE Finance.ApPayment ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH('Finance.ApPayment', 'StatusCode') IS NULL ALTER TABLE Finance.ApPayment ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.ApPayment', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.ApPayment ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.ApPayment', 'CreatedByUserId') IS NULL ALTER TABLE Finance.ApPayment ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApPayment', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.ApPayment ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.ApPayment', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.ApPayment ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApPayment', 'IsDeleted') IS NULL ALTER TABLE Finance.ApPayment ADD IsDeleted bit NULL;

UPDATE Finance.ApPayment SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.ApPayment SET PaymentDate = COALESCE(PaymentDate, CONVERT(date, SYSUTCDATETIME())) WHERE PaymentDate IS NULL;
UPDATE Finance.ApPayment SET Amount = COALESCE(Amount, 0) WHERE Amount IS NULL;
UPDATE Finance.ApPayment SET PaymentMethodCode = COALESCE(NULLIF(PaymentMethodCode, ''), 'ACH') WHERE PaymentMethodCode IS NULL OR PaymentMethodCode = '';
UPDATE Finance.ApPayment SET StatusCode = COALESCE(NULLIF(StatusCode, ''), 'Pending') WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.ApPayment SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.ApPayment SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApPayment_Tenant_Date' AND object_id = OBJECT_ID('Finance.ApPayment'))
    CREATE INDEX IX_ApPayment_Tenant_Date ON Finance.ApPayment (TenantId, PaymentDate DESC) INCLUDE (VendorId, ApInvoiceId, Amount, PaymentMethodCode, StatusCode);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Finance.Vendor WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO Finance.Vendor (VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, 'VEN-INS-001', 'Summit Claims Services', 'Maya Torres', 'billing@summitclaims.example', '(555) 014-2201', 'Net30', 'USD', '82-1456789', 'Service Provider', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, 'VEN-OFF-002', 'Northstar Office Supply', 'Daniel Reeves', 'ap@northstaroffice.example', '(555) 014-4430', 'Net15', 'USD', '41-9023456', 'Supplier', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;

DECLARE @VendorA uniqueidentifier = (SELECT TOP (1) VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0 ORDER BY VendorName);
DECLARE @InvoiceA uniqueidentifier = (SELECT TOP (1) ApInvoiceId FROM Finance.ApInvoice WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0 ORDER BY InvoiceDate DESC);

IF @InvoiceA IS NULL AND @VendorA IS NOT NULL
BEGIN
    SET @InvoiceA = NEWID();
    INSERT INTO Finance.ApInvoice (ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, Description, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (@InvoiceA, @TenantId, @VendorA, 'AP-2025-2001', DATEADD(day, -10, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, 20, CONVERT(date, SYSUTCDATETIME())), 3200.00, 0.00, 0.00, 'Open', NULL, NULL, 'Seed AP invoice for payment workflow.', 'Created automatically for AP payment workflow.', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;

IF NOT EXISTS (SELECT 1 FROM Finance.ApPayment WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0) AND @VendorA IS NOT NULL
BEGIN
    INSERT INTO Finance.ApPayment (ApPaymentId, TenantId, VendorId, ApInvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, Notes, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @VendorA, @InvoiceA, DATEADD(day, -3, CONVERT(date, SYSUTCDATETIME())), 1280.50, 'ACH', 'ACH-2025-4187', 'Processed through AP payment batch.', 'Processed', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @VendorA, NULL, CONVERT(date, SYSUTCDATETIME()), 850.00, 'Check', 'CHK-10422', 'Pending check approval.', 'Pending', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @VendorA, @InvoiceA, DATEADD(day, 2, CONVERT(date, SYSUTCDATETIME())), 2400.00, 'Wire', 'WIRE-SCHED-772', 'Scheduled wire transfer.', 'Pending', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<ApPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = @"
SELECT
    ApPaymentId,
    TenantId,
    VendorId,
    ApInvoiceId,
    PaymentDate,
    Amount,
    PaymentMethodCode,
    ReferenceNumber,
    Notes,
    StatusCode,
    CreatedDateUtc
FROM Finance.ApPayment
WHERE ApPaymentId = @Id AND COALESCE(IsDeleted, 0) = 0";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ApPaymentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ApPaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "ApPaymentId, TenantId, VendorId, ApInvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, Notes, StatusCode, CreatedDateUtc";
        var searchPredicate = "ReferenceNumber LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%' OR PaymentMethodCode LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.ApPayment", selectColumns, searchPredicate, "PaymentDate DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ApPaymentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ApPaymentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateApPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.ApPayment (ApPaymentId, TenantId, VendorId, ApInvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, Notes, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @VendorId, @ApInvoiceId, @PaymentDate, @Amount, @PaymentMethodCode, @ReferenceNumber, @Notes, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.VendorId, request.ApInvoiceId, request.PaymentDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.Notes, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        await SyncProcessedPaymentToInvoiceAsync(cn, request.TenantId, request.ApInvoiceId, request.Amount, request.StatusCode, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateApPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.ApPayment
SET VendorId = @VendorId,
    ApInvoiceId = @ApInvoiceId,
    PaymentDate = @PaymentDate,
    Amount = @Amount,
    PaymentMethodCode = @PaymentMethodCode,
    ReferenceNumber = @ReferenceNumber,
    Notes = @Notes,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ApPaymentId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.VendorId, request.ApInvoiceId, request.PaymentDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.Notes, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        await SyncProcessedPaymentToInvoiceAsync(cn, request.TenantId, request.ApInvoiceId, request.Amount, request.StatusCode, cancellationToken);
    }

    private static async Task SyncProcessedPaymentToInvoiceAsync(System.Data.IDbConnection connection, Guid tenantId, Guid? apInvoiceId, decimal amount, string statusCode, CancellationToken cancellationToken)
    {
        if (apInvoiceId is null || apInvoiceId == Guid.Empty || amount <= 0 || !string.Equals(statusCode, "Processed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const string sql = @"
IF OBJECT_ID(N'Finance.ApInvoice', N'U') IS NOT NULL
BEGIN
    UPDATE Finance.ApInvoice
    SET AmountPaid = CASE WHEN AmountPaid + @Amount > Amount THEN Amount ELSE AmountPaid + @Amount END,
        StatusCode = CASE WHEN AmountPaid + @Amount >= Amount THEN N'Paid' ELSE StatusCode END,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE ApInvoiceId = @ApInvoiceId
      AND TenantId = @TenantId
      AND COALESCE(IsDeleted, 0) = 0;
END;";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ApInvoiceId = apInvoiceId, Amount = amount }, cancellationToken: cancellationToken));
    }
}