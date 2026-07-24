using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionTransactionRepository : ICommissionTransactionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionTransactionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = "SELECT TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc FROM Commission.CommissionTransaction WHERE TransactionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionTransactionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionTransactionDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionTransaction", "TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc", "SourceEntityName LIKE '%' + @SearchTerm + '%'", "TransactionDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionTransactionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionTransactionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<CommissionLedgerRowDto>> SearchLedgerAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        const string sql = @"
SELECT CommissionId,
       TenantId,
       PolicyNumber,
       Period,
       BusinessType,
       Producer,
       AccountName,
       LineOfBusiness,
       Carrier,
       GrossAmount,
       CommissionPct,
       AgencyAmount,
       ProducerAmount,
       Status,
       StatementNumber,
       PayoutBatch,
       TransactionDate,
       PaidDate
FROM Commission.CommissionLedger
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N''
       OR PolicyNumber LIKE N'%' + @SearchTerm + N'%'
       OR Producer LIKE N'%' + @SearchTerm + N'%'
       OR AccountName LIKE N'%' + @SearchTerm + N'%'
       OR Carrier LIKE N'%' + @SearchTerm + N'%'
       OR StatementNumber LIKE N'%' + @SearchTerm + N'%'
       OR PayoutBatch LIKE N'%' + @SearchTerm + N'%'
       OR BusinessType LIKE N'%' + @SearchTerm + N'%'
       OR Period LIKE N'%' + @SearchTerm + N'%')
ORDER BY TransactionDate DESC, PolicyNumber;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<CommissionLedgerRowDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CommissionLedgerRowDto?> GetLedgerByIdAsync(Guid tenantId, Guid commissionId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

        const string sql = @"
SELECT CommissionId,
       TenantId,
       PolicyNumber,
       Period,
       BusinessType,
       Producer,
       AccountName,
       LineOfBusiness,
       Carrier,
       GrossAmount,
       CommissionPct,
       AgencyAmount,
       ProducerAmount,
       Status,
       StatementNumber,
       PayoutBatch,
       TransactionDate,
       PaidDate
FROM Commission.CommissionLedger
WHERE TenantId = @TenantId
  AND CommissionId = @CommissionId
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionLedgerRowDto>(new CommandDefinition(sql, new { TenantId = tenantId, CommissionId = commissionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateLedgerAsync(CreateCommissionLedgerRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        var agencyAmount = request.AgencyAmount != 0m ? request.AgencyAmount : Math.Round(request.GrossAmount * request.CommissionPct / 100m, 2);
        const string sql = @"
INSERT INTO Commission.CommissionLedger
    (CommissionId, TenantId, PolicyNumber, Period, BusinessType, Producer, AccountName, LineOfBusiness, Carrier, GrossAmount, CommissionPct, AgencyAmount, ProducerAmount, Status, StatementNumber, PayoutBatch, TransactionDate, PaidDate, CreatedDateUtc, IsDeleted)
VALUES
    (@CommissionId, @TenantId, @PolicyNumber, @Period, @BusinessType, @Producer, @AccountName, @LineOfBusiness, @Carrier, @GrossAmount, @CommissionPct, @AgencyAmount, @ProducerAmount, @Status, @StatementNumber, @PayoutBatch, @TransactionDate, @PaidDate, SYSUTCDATETIME(), 0);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CommissionId = id,
            request.TenantId,
            PolicyNumber = request.PolicyNumber.Trim(),
            Period = request.Period.Trim(),
            BusinessType = request.BusinessType.Trim(),
            Producer = request.Producer.Trim(),
            AccountName = request.AccountName.Trim(),
            LineOfBusiness = request.LineOfBusiness.Trim(),
            Carrier = request.Carrier.Trim(),
            request.GrossAmount,
            request.CommissionPct,
            AgencyAmount = agencyAmount,
            request.ProducerAmount,
            Status = request.Status.Trim(),
            StatementNumber = request.StatementNumber.Trim(),
            PayoutBatch = request.PayoutBatch.Trim(),
            request.TransactionDate,
            request.PaidDate,
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<Guid> CreateAsync(CreateCommissionTransactionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var id = Guid.NewGuid();
        var amount = request.CommissionAmount > 0 ? request.CommissionAmount : Math.Round(request.GrossAmount * request.CommissionRate / 100m, 2);
        const string sql = @"
INSERT INTO Commission.CommissionTransaction (TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @PayeeId, @CommissionPlanId, @SourceEntityName, @SourceEntityId, @TransactionDate, @GrossAmount, @CommissionRate, @CommissionAmount, @StatusCode, @PayoutId, SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PayeeId, request.CommissionPlanId, request.SourceEntityName, request.SourceEntityId, request.TransactionDate, request.GrossAmount, request.CommissionRate, CommissionAmount = amount, request.StatusCode, request.PayoutId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionTransactionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);

        var amount = request.CommissionAmount > 0 ? request.CommissionAmount : Math.Round(request.GrossAmount * request.CommissionRate / 100m, 2);
        const string sql = @"
UPDATE Commission.CommissionTransaction
SET PayeeId = @PayeeId,
    CommissionPlanId = @CommissionPlanId,
    SourceEntityName = @SourceEntityName,
    SourceEntityId = @SourceEntityId,
    TransactionDate = @TransactionDate,
    GrossAmount = @GrossAmount,
    CommissionRate = @CommissionRate,
    CommissionAmount = @CommissionAmount,
    StatusCode = @StatusCode,
    PayoutId = @PayoutId
WHERE TransactionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.PayeeId, request.CommissionPlanId, request.SourceEntityName, request.SourceEntityId, request.TransactionDate, request.GrossAmount, request.CommissionRate, CommissionAmount = amount, request.StatusCode, request.PayoutId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionPlan', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPlan (CommissionPlanId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PlanCode NVARCHAR(50) NOT NULL, PlanName NVARCHAR(200) NOT NULL, PlanTypeCode NVARCHAR(50) NOT NULL DEFAULT N'Standard', NewBusinessRatePct DECIMAL(9,4) NOT NULL DEFAULT 0, RenewalRatePct DECIMAL(9,4) NOT NULL DEFAULT 0, EffectiveStartDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()), StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft', AllowSplit BIT NOT NULL DEFAULT 0, HouseAccountRules BIT NOT NULL DEFAULT 0, BranchOverrideEligible BIT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Commission.CommissionPayee', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPayee (PayeeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, UserId UNIQUEIDENTIFIER NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, PayeeTypeCode NVARCHAR(50) NOT NULL, SplitPercentage DECIMAL(9,4) NOT NULL DEFAULT 100, EffectiveDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()), StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Active', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayee ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTxPayee_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'UserId') IS NULL ALTER TABLE Commission.CommissionPayee ADD UserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeTypeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeTypeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'SplitPercentage') IS NULL ALTER TABLE Commission.CommissionPayee ADD SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionTxPayee_Split DEFAULT 100;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'EffectiveDate') IS NULL ALTER TABLE Commission.CommissionPayee ADD EffectiveDate DATE NOT NULL CONSTRAINT DF_CommissionTxPayee_Effective DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayee', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionTxPayee_Status DEFAULT N'Active';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionPayee ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionTxPayee_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionPayee', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionTxPayee_IsDeleted DEFAULT 0;
END;

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionTransaction (TransactionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, SourceEntityName NVARCHAR(100) NOT NULL, SourceEntityId UNIQUEIDENTIFIER NOT NULL, TransactionDate DATE NOT NULL, GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0, CommissionRate DECIMAL(9,4) NOT NULL DEFAULT 0, CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', PayoutId UNIQUEIDENTIFIER NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TransactionId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TransactionId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TenantId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTx_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'SourceEntityName') IS NULL ALTER TABLE Commission.CommissionTransaction ADD SourceEntityName NVARCHAR(100) NOT NULL CONSTRAINT DF_CommissionTx_SourceEntityName DEFAULT N'Policy';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'SourceEntityId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD SourceEntityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTx_SourceEntityId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TransactionDate') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TransactionDate DATE NOT NULL CONSTRAINT DF_CommissionTx_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'GrossAmount') IS NULL ALTER TABLE Commission.CommissionTransaction ADD GrossAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionTx_Gross DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionRate') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionRate DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionTx_Rate DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionAmount') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionTx_Amount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionTransaction ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionTx_Status DEFAULT N'Pending';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'PayoutId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD PayoutId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionTx_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionTransaction ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionTx_IsDeleted DEFAULT 0;
END;

IF OBJECT_ID(N'Commission.CommissionLedger', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionLedger (CommissionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PolicyNumber NVARCHAR(80) NOT NULL, Period NVARCHAR(50) NOT NULL, BusinessType NVARCHAR(100) NOT NULL, Producer NVARCHAR(200) NOT NULL, AccountName NVARCHAR(200) NOT NULL, LineOfBusiness NVARCHAR(100) NOT NULL, Carrier NVARCHAR(200) NOT NULL, GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0, CommissionPct DECIMAL(9,4) NOT NULL DEFAULT 0, AgencyAmount DECIMAL(18,2) NOT NULL DEFAULT 0, ProducerAmount DECIMAL(18,2) NOT NULL DEFAULT 0, Status NVARCHAR(50) NOT NULL DEFAULT N'Pending', StatementNumber NVARCHAR(80) NOT NULL DEFAULT N'', PayoutBatch NVARCHAR(80) NOT NULL DEFAULT N'', TransactionDate DATE NOT NULL DEFAULT CONVERT(date, SYSUTCDATETIME()), PaidDate DATE NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionLedger', N'CommissionId') IS NULL ALTER TABLE Commission.CommissionLedger ADD CommissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionLedger_Id DEFAULT NEWID();
    IF COL_LENGTH(N'Commission.CommissionLedger', N'TenantId') IS NULL ALTER TABLE Commission.CommissionLedger ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionLedger_Tenant DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'PolicyNumber') IS NULL ALTER TABLE Commission.CommissionLedger ADD PolicyNumber NVARCHAR(80) NOT NULL CONSTRAINT DF_CommissionLedger_Policy DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'Period') IS NULL ALTER TABLE Commission.CommissionLedger ADD Period NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionLedger_Period DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'BusinessType') IS NULL ALTER TABLE Commission.CommissionLedger ADD BusinessType NVARCHAR(100) NOT NULL CONSTRAINT DF_CommissionLedger_BusinessType DEFAULT N'Policy';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'Producer') IS NULL ALTER TABLE Commission.CommissionLedger ADD Producer NVARCHAR(200) NOT NULL CONSTRAINT DF_CommissionLedger_Producer DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'AccountName') IS NULL ALTER TABLE Commission.CommissionLedger ADD AccountName NVARCHAR(200) NOT NULL CONSTRAINT DF_CommissionLedger_Account DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'LineOfBusiness') IS NULL ALTER TABLE Commission.CommissionLedger ADD LineOfBusiness NVARCHAR(100) NOT NULL CONSTRAINT DF_CommissionLedger_Lob DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'Carrier') IS NULL ALTER TABLE Commission.CommissionLedger ADD Carrier NVARCHAR(200) NOT NULL CONSTRAINT DF_CommissionLedger_Carrier DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'GrossAmount') IS NULL ALTER TABLE Commission.CommissionLedger ADD GrossAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionLedger_Gross DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionLedger', N'CommissionPct') IS NULL ALTER TABLE Commission.CommissionLedger ADD CommissionPct DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionLedger_Pct DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionLedger', N'AgencyAmount') IS NULL ALTER TABLE Commission.CommissionLedger ADD AgencyAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionLedger_Agency DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionLedger', N'ProducerAmount') IS NULL ALTER TABLE Commission.CommissionLedger ADD ProducerAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionLedger_ProducerAmount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionLedger', N'Status') IS NULL ALTER TABLE Commission.CommissionLedger ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionLedger_Status DEFAULT N'Pending';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'StatementNumber') IS NULL ALTER TABLE Commission.CommissionLedger ADD StatementNumber NVARCHAR(80) NOT NULL CONSTRAINT DF_CommissionLedger_Statement DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'PayoutBatch') IS NULL ALTER TABLE Commission.CommissionLedger ADD PayoutBatch NVARCHAR(80) NOT NULL CONSTRAINT DF_CommissionLedger_Payout DEFAULT N'';
    IF COL_LENGTH(N'Commission.CommissionLedger', N'TransactionDate') IS NULL ALTER TABLE Commission.CommissionLedger ADD TransactionDate DATE NOT NULL CONSTRAINT DF_CommissionLedger_TxDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionLedger', N'PaidDate') IS NULL ALTER TABLE Commission.CommissionLedger ADD PaidDate DATE NULL;
    IF COL_LENGTH(N'Commission.CommissionLedger', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionLedger ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionLedger_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionLedger', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionLedger ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionLedger', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionLedger ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionLedger_IsDeleted DEFAULT 0;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Commission.CommissionLedger') AND name = N'IX_CommissionLedger_Tenant_Search_Runtime')
    CREATE INDEX IX_CommissionLedger_Tenant_Search_Runtime ON Commission.CommissionLedger(TenantId, IsDeleted, TransactionDate DESC) INCLUDE (PolicyNumber, Producer, AccountName, Carrier, Status, StatementNumber, PayoutBatch);

";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
