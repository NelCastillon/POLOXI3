using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BillingAdjustmentRepository : IBillingAdjustmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public BillingAdjustmentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance')
    EXEC(N'CREATE SCHEMA Finance');

IF OBJECT_ID(N'Finance.BillingAdjustment', N'U') IS NULL
BEGIN
    CREATE TABLE Finance.BillingAdjustment
    (
        AdjustmentId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId           UNIQUEIDENTIFIER NOT NULL,
        InvoiceId          UNIQUEIDENTIFIER NOT NULL,
        AccountId          UNIQUEIDENTIFIER NOT NULL,
        AdjustmentTypeCode NVARCHAR(80)     NOT NULL,
        AdjustmentDate     DATE             NOT NULL,
        Amount             DECIMAL(18,2)    NOT NULL,
        Reason             NVARCHAR(1000)   NOT NULL,
        ApprovedByUserId   UNIQUEIDENTIFIER NULL,
        ApprovedDateUtc    DATETIME2        NULL,
        StatusCode         NVARCHAR(50)     NOT NULL DEFAULT N'Pending',
        CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId    UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc    DATETIME2        NULL,
        ModifiedByUserId   UNIQUEIDENTIFIER NULL,
        IsDeleted          BIT              NOT NULL DEFAULT 0
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'TenantId') IS NULL ALTER TABLE Finance.BillingAdjustment ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BillingAdjustment_TenantId DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'InvoiceId') IS NULL ALTER TABLE Finance.BillingAdjustment ADD InvoiceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BillingAdjustment_InvoiceId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'AccountId') IS NULL ALTER TABLE Finance.BillingAdjustment ADD AccountId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_BillingAdjustment_AccountId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'AdjustmentTypeCode') IS NULL ALTER TABLE Finance.BillingAdjustment ADD AdjustmentTypeCode NVARCHAR(80) NOT NULL CONSTRAINT DF_BillingAdjustment_Type DEFAULT N'Credit';
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'AdjustmentDate') IS NULL ALTER TABLE Finance.BillingAdjustment ADD AdjustmentDate DATE NOT NULL CONSTRAINT DF_BillingAdjustment_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'Amount') IS NULL ALTER TABLE Finance.BillingAdjustment ADD Amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_BillingAdjustment_Amount DEFAULT 0;
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'Reason') IS NULL ALTER TABLE Finance.BillingAdjustment ADD Reason NVARCHAR(1000) NOT NULL CONSTRAINT DF_BillingAdjustment_Reason DEFAULT N'';
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'ApprovedByUserId') IS NULL ALTER TABLE Finance.BillingAdjustment ADD ApprovedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'ApprovedDateUtc') IS NULL ALTER TABLE Finance.BillingAdjustment ADD ApprovedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'StatusCode') IS NULL ALTER TABLE Finance.BillingAdjustment ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BillingAdjustment_Status DEFAULT N'Pending';
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'CreatedDateUtc') IS NULL ALTER TABLE Finance.BillingAdjustment ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingAdjustment_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'CreatedByUserId') IS NULL ALTER TABLE Finance.BillingAdjustment ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'ModifiedDateUtc') IS NULL ALTER TABLE Finance.BillingAdjustment ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'ModifiedByUserId') IS NULL ALTER TABLE Finance.BillingAdjustment ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Finance.BillingAdjustment', N'IsDeleted') IS NULL ALTER TABLE Finance.BillingAdjustment ADD IsDeleted BIT NOT NULL CONSTRAINT DF_BillingAdjustment_IsDeleted DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Finance.BillingAdjustment') AND name = N'IX_BillingAdjustment_Tenant_Date')
    CREATE INDEX IX_BillingAdjustment_Tenant_Date ON Finance.BillingAdjustment(TenantId, AdjustmentDate DESC, IsDeleted);

IF @TenantId IS NOT NULL
   AND @TenantId <> '00000000-0000-0000-0000-000000000000'
   AND NOT EXISTS (SELECT 1 FROM Finance.BillingAdjustment WHERE TenantId = @TenantId)
   AND OBJECT_ID(N'Billing.Invoice', N'U') IS NOT NULL
BEGIN
    INSERT INTO Finance.BillingAdjustment (AdjustmentId, TenantId, InvoiceId, AccountId, AdjustmentTypeCode, AdjustmentDate, Amount, Reason, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc, IsDeleted)
    SELECT TOP (3)
        NEWID(),
        i.TenantId,
        i.InvoiceId,
        i.AccountId,
        CASE ROW_NUMBER() OVER (ORDER BY i.CreatedDateUtc DESC) WHEN 1 THEN N'Premium Refund' WHEN 2 THEN N'Endorsement Credit' ELSE N'Billing Adjustment' END,
        CONVERT(date, SYSUTCDATETIME()),
        CASE WHEN i.BalanceAmount > 0 THEN i.BalanceAmount * 0.05 ELSE i.TotalAmount * 0.03 END,
        N'Tenant Admin seed credit synchronized from billing invoice data.',
        NULL,
        NULL,
        N'Pending',
        SYSUTCDATETIME(),
        0
    FROM Billing.Invoice i
    WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.TotalAmount > 0
    ORDER BY i.CreatedDateUtc DESC;
END";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<BillingAdjustmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "SELECT AdjustmentId, TenantId, InvoiceId, AccountId, AdjustmentTypeCode, AdjustmentDate, Amount, Reason, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc FROM Finance.BillingAdjustment WHERE AdjustmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BillingAdjustmentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BillingAdjustmentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Finance.BillingAdjustment", "AdjustmentId, TenantId, InvoiceId, AccountId, AdjustmentTypeCode, AdjustmentDate, Amount, Reason, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc", "Reason LIKE '%' + @SearchTerm + '%' OR AdjustmentTypeCode LIKE '%' + @SearchTerm + '%'", "AdjustmentDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BillingAdjustmentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BillingAdjustmentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateBillingAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.BillingAdjustment (AdjustmentId, TenantId, InvoiceId, AccountId, AdjustmentTypeCode, AdjustmentDate, Amount, Reason, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @InvoiceId, @AccountId, @AdjustmentTypeCode, @AdjustmentDate, @Amount, @Reason, @ApprovedByUserId, @ApprovedDateUtc, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.InvoiceId, request.AccountId, request.AdjustmentTypeCode, request.AdjustmentDate, request.Amount, request.Reason, request.ApprovedByUserId, request.ApprovedDateUtc, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateBillingAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = @"
UPDATE Finance.BillingAdjustment
SET InvoiceId = @InvoiceId,
    AccountId = @AccountId,
    AdjustmentTypeCode = @AdjustmentTypeCode,
    AdjustmentDate = @AdjustmentDate,
    Amount = @Amount,
    Reason = @Reason,
    ApprovedByUserId = @ApprovedByUserId,
    ApprovedDateUtc = @ApprovedDateUtc,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AdjustmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.InvoiceId, request.AccountId, request.AdjustmentTypeCode, request.AdjustmentDate, request.Amount, request.Reason, request.ApprovedByUserId, request.ApprovedDateUtc, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(cancellationToken: cancellationToken);
        const string sql = "UPDATE Finance.BillingAdjustment SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE AdjustmentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
