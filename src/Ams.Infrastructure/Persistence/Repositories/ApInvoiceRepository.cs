using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ApInvoiceRepository : IApInvoiceRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ApInvoiceRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

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

IF COL_LENGTH('Finance.ApInvoice', 'TenantId') IS NULL ALTER TABLE Finance.ApInvoice ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApInvoice', 'VendorId') IS NULL ALTER TABLE Finance.ApInvoice ADD VendorId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApInvoice', 'InvoiceNumber') IS NULL ALTER TABLE Finance.ApInvoice ADD InvoiceNumber nvarchar(80) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'InvoiceDate') IS NULL ALTER TABLE Finance.ApInvoice ADD InvoiceDate date NULL;
IF COL_LENGTH('Finance.ApInvoice', 'DueDate') IS NULL ALTER TABLE Finance.ApInvoice ADD DueDate date NULL;
IF COL_LENGTH('Finance.ApInvoice', 'Amount') IS NULL ALTER TABLE Finance.ApInvoice ADD Amount decimal(18,2) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'AmountPaid') IS NULL ALTER TABLE Finance.ApInvoice ADD AmountPaid decimal(18,2) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'TaxAmount') IS NULL ALTER TABLE Finance.ApInvoice ADD TaxAmount decimal(18,2) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'StatusCode') IS NULL ALTER TABLE Finance.ApInvoice ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'GLAccountId') IS NULL ALTER TABLE Finance.ApInvoice ADD GLAccountId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApInvoice', 'AgreementId') IS NULL ALTER TABLE Finance.ApInvoice ADD AgreementId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApInvoice', 'Description') IS NULL ALTER TABLE Finance.ApInvoice ADD Description nvarchar(1000) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'Notes') IS NULL ALTER TABLE Finance.ApInvoice ADD Notes nvarchar(1000) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.ApInvoice ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'CreatedByUserId') IS NULL ALTER TABLE Finance.ApInvoice ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApInvoice', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.ApInvoice ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.ApInvoice', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.ApInvoice ADD ModifiedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.ApInvoice', 'IsDeleted') IS NULL ALTER TABLE Finance.ApInvoice ADD IsDeleted bit NULL;

UPDATE Finance.ApInvoice SET TenantId = COALESCE(TenantId, @TenantId) WHERE TenantId IS NULL AND @TenantId IS NOT NULL;
UPDATE Finance.ApInvoice SET InvoiceNumber = COALESCE(NULLIF(InvoiceNumber, ''), CONCAT('AP-', RIGHT(CONVERT(varchar(36), ApInvoiceId), 6))) WHERE InvoiceNumber IS NULL OR InvoiceNumber = '';
UPDATE Finance.ApInvoice SET InvoiceDate = COALESCE(InvoiceDate, CONVERT(date, SYSUTCDATETIME())) WHERE InvoiceDate IS NULL;
UPDATE Finance.ApInvoice SET DueDate = COALESCE(DueDate, DATEADD(day, 30, COALESCE(InvoiceDate, CONVERT(date, SYSUTCDATETIME())))) WHERE DueDate IS NULL;
UPDATE Finance.ApInvoice SET Amount = COALESCE(Amount, 0) WHERE Amount IS NULL;
UPDATE Finance.ApInvoice SET AmountPaid = COALESCE(AmountPaid, 0) WHERE AmountPaid IS NULL;
UPDATE Finance.ApInvoice SET TaxAmount = COALESCE(TaxAmount, 0) WHERE TaxAmount IS NULL;
UPDATE Finance.ApInvoice SET StatusCode = COALESCE(NULLIF(StatusCode, ''), CASE WHEN COALESCE(AmountPaid, 0) >= COALESCE(Amount, 0) AND COALESCE(Amount, 0) > 0 THEN 'Paid' ELSE 'Open' END) WHERE StatusCode IS NULL OR StatusCode = '';
UPDATE Finance.ApInvoice SET CreatedDateUtc = COALESCE(CreatedDateUtc, SYSUTCDATETIME()) WHERE CreatedDateUtc IS NULL;
UPDATE Finance.ApInvoice SET IsDeleted = COALESCE(IsDeleted, 0) WHERE IsDeleted IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApInvoice_Tenant_Date' AND object_id = OBJECT_ID('Finance.ApInvoice'))
    CREATE INDEX IX_ApInvoice_Tenant_Date ON Finance.ApInvoice (TenantId, InvoiceDate DESC) INCLUDE (InvoiceNumber, VendorId, DueDate, Amount, AmountPaid, StatusCode);
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
        (NEWID(), @TenantId, 'VEN-OFF-002', 'Northstar Office Supply', 'Daniel Reeves', 'ap@northstaroffice.example', '(555) 014-4430', 'Net15', 'USD', '41-9023456', 'Supplier', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, 'VEN-TEC-003', 'Harbor IT Managed Services', 'Priya Shah', 'finance@harborit.example', '(555) 014-7788', 'Net45', 'USD', '65-7788990', 'Contractor', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;

DECLARE @VendorA uniqueidentifier = (SELECT TOP (1) VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0 ORDER BY VendorName);
DECLARE @VendorB uniqueidentifier = (SELECT TOP (1) VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0 AND VendorId <> @VendorA ORDER BY VendorName DESC);
SET @VendorB = COALESCE(@VendorB, @VendorA);

IF NOT EXISTS (SELECT 1 FROM Finance.ApInvoice WHERE TenantId = @TenantId AND COALESCE(IsDeleted, 0) = 0)
BEGIN
    INSERT INTO Finance.ApInvoice (ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, Description, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @VendorA, 'AP-2025-1001', DATEADD(day, -18, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, 12, CONVERT(date, SYSUTCDATETIME())), 4250.00, 0.00, 0.00, 'Open', NULL, NULL, 'Claims adjusting services for commercial accounts.', 'Review supporting documents before payment run.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @VendorB, 'AP-2025-1002', DATEADD(day, -34, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, -4, CONVERT(date, SYSUTCDATETIME())), 1280.50, 1280.50, 82.50, 'Paid', NULL, NULL, 'Office supplies and print materials.', 'Paid through ACH batch.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @VendorA, 'AP-2025-1003', DATEADD(day, -6, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, 24, CONVERT(date, SYSUTCDATETIME())), 7600.00, 2500.00, 0.00, 'Pending', NULL, NULL, 'Managed services monthly retainer.', 'Partial payment applied.', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<ApInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = @"
SELECT
    ApInvoiceId,
    TenantId,
    VendorId,
    InvoiceNumber,
    InvoiceDate,
    DueDate,
    Amount,
    AmountPaid,
    TaxAmount,
    StatusCode,
    GLAccountId,
    AgreementId,
    Description,
    Notes,
    CreatedDateUtc
FROM Finance.ApInvoice
WHERE ApInvoiceId = @Id AND COALESCE(IsDeleted, 0) = 0";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ApInvoiceDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ApInvoiceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, Description, Notes, CreatedDateUtc";
        var searchPredicate = "InvoiceNumber LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";

        var sql = RepositorySql.BuildPagedSearchSql("Finance.ApInvoice", selectColumns, searchPredicate, "InvoiceDate DESC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ApInvoiceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ApInvoiceDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.ApInvoice (ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, Description, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @VendorId, @InvoiceNumber, @InvoiceDate, @DueDate, @Amount, @AmountPaid, @TaxAmount, @StatusCode, @GLAccountId, @AgreementId, @Description, @Notes, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.VendorId, request.InvoiceNumber, request.InvoiceDate, request.DueDate, request.Amount, request.AmountPaid, request.TaxAmount, request.StatusCode, request.GLAccountId, request.AgreementId, request.Description, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.ApInvoice
SET VendorId = @VendorId,
    InvoiceNumber = @InvoiceNumber,
    InvoiceDate = @InvoiceDate,
    DueDate = @DueDate,
    Amount = @Amount,
    AmountPaid = @AmountPaid,
    TaxAmount = @TaxAmount,
    StatusCode = @StatusCode,
    GLAccountId = @GLAccountId,
    AgreementId = @AgreementId,
    Description = @Description,
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ApInvoiceId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.VendorId, request.InvoiceNumber, request.InvoiceDate, request.DueDate, request.Amount, request.AmountPaid, request.TaxAmount, request.StatusCode, request.GLAccountId, request.AgreementId, request.Description, request.Notes, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}