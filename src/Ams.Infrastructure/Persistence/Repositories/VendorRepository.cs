using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class VendorRepository : IVendorRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public VendorRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

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
IF COL_LENGTH('Finance.Vendor', 'ContactName') IS NULL ALTER TABLE Finance.Vendor ADD ContactName nvarchar(150) NULL;
IF COL_LENGTH('Finance.Vendor', 'Email') IS NULL ALTER TABLE Finance.Vendor ADD Email nvarchar(254) NULL;
IF COL_LENGTH('Finance.Vendor', 'Phone') IS NULL ALTER TABLE Finance.Vendor ADD Phone nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'PaymentTermsCode') IS NULL ALTER TABLE Finance.Vendor ADD PaymentTermsCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'CurrencyCode') IS NULL ALTER TABLE Finance.Vendor ADD CurrencyCode nvarchar(3) NULL;
IF COL_LENGTH('Finance.Vendor', 'TaxId') IS NULL ALTER TABLE Finance.Vendor ADD TaxId nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'VendorTypeCode') IS NULL ALTER TABLE Finance.Vendor ADD VendorTypeCode nvarchar(80) NULL;
IF COL_LENGTH('Finance.Vendor', 'StatusCode') IS NULL ALTER TABLE Finance.Vendor ADD StatusCode nvarchar(50) NULL;
IF COL_LENGTH('Finance.Vendor', 'CreatedDateUtc') IS NULL ALTER TABLE Finance.Vendor ADD CreatedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.Vendor', 'CreatedByUserId') IS NULL ALTER TABLE Finance.Vendor ADD CreatedByUserId uniqueidentifier NULL;
IF COL_LENGTH('Finance.Vendor', 'ModifiedDateUtc') IS NULL ALTER TABLE Finance.Vendor ADD ModifiedDateUtc datetime2(7) NULL;
IF COL_LENGTH('Finance.Vendor', 'ModifiedByUserId') IS NULL ALTER TABLE Finance.Vendor ADD ModifiedByUserId uniqueidentifier NULL;
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Vendor_Tenant_Name' AND object_id = OBJECT_ID('Finance.Vendor'))
    CREATE INDEX IX_Vendor_Tenant_Name ON Finance.Vendor (TenantId, VendorName) INCLUDE (VendorCode, VendorTypeCode, StatusCode, PaymentTermsCode);
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            return;
        }

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Finance.Vendor WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Finance.Vendor (VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, 'VEN-INS-001', 'Summit Claims Services', 'Maya Torres', 'billing@summitclaims.example', '(555) 014-2201', 'Net30', 'USD', '82-1456789', 'Service Provider', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, 'VEN-OFF-002', 'Northstar Office Supply', 'Daniel Reeves', 'ap@northstaroffice.example', '(555) 014-4430', 'Net15', 'USD', '41-9023456', 'Supplier', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, 'VEN-TEC-003', 'Harbor IT Managed Services', 'Priya Shah', 'finance@harborit.example', '(555) 014-7788', 'Net45', 'USD', '65-7788990', 'Contractor', 'Active', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, 'VEN-ARC-004', 'Archive Storage Partners', 'Ethan Cole', 'accounts@archivestorage.example', '(555) 014-9912', 'Net60', 'USD', '77-3401928', 'Service Provider', 'Inactive', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;
";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId.Value }, cancellationToken: cancellationToken));
    }

    public async Task<VendorDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);

        const string sql = @"
SELECT
    VendorId,
    TenantId,
    VendorCode,
    VendorName,
    ContactName,
    Email,
    Phone,
    PaymentTermsCode,
    CurrencyCode,
    TaxId,
    VendorTypeCode,
    StatusCode,
    CreatedDateUtc
FROM Finance.Vendor
WHERE VendorId = @Id AND COALESCE(IsDeleted, 0) = 0";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<VendorDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<VendorDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        var selectColumns = "VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc";
        var searchPredicate = "VendorCode LIKE '%' + @SearchTerm + '%' OR VendorName LIKE '%' + @SearchTerm + '%' OR VendorTypeCode LIKE '%' + @SearchTerm + '%' OR Email LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.Vendor", selectColumns, searchPredicate, "VendorName ASC");

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<VendorDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<VendorDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateVendorRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.Vendor (VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @VendorCode, @VendorName, @ContactName, @Email, @Phone, @PaymentTermsCode, @CurrencyCode, @TaxId, @VendorTypeCode, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.VendorCode, request.VendorName, request.ContactName, request.Email, request.Phone, request.PaymentTermsCode, request.CurrencyCode, request.TaxId, request.VendorTypeCode, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateVendorRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        const string sql = @"
UPDATE Finance.Vendor
SET VendorCode = @VendorCode,
    VendorName = @VendorName,
    ContactName = @ContactName,
    Email = @Email,
    Phone = @Phone,
    PaymentTermsCode = @PaymentTermsCode,
    CurrencyCode = @CurrencyCode,
    TaxId = @TaxId,
    VendorTypeCode = @VendorTypeCode,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE VendorId = @Id AND COALESCE(IsDeleted, 0) = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.VendorCode, request.VendorName, request.ContactName, request.Email, request.Phone, request.PaymentTermsCode, request.CurrencyCode, request.TaxId, request.VendorTypeCode, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}