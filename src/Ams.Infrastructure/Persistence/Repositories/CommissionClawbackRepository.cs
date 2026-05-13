using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionClawbackRepository : ICommissionClawbackRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionClawbackRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionClawbackDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = @"SELECT ClawbackId, TenantId, PayeeId, OriginalTransactionId, ClawbackDate, Amount, ReasonCode, Notes, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc FROM Commission.CommissionClawback WHERE ClawbackId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionClawbackDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionClawbackDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionClawback",
            "ClawbackId, TenantId, PayeeId, OriginalTransactionId, ClawbackDate, Amount, ReasonCode, Notes, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc",
            "ReasonCode LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionClawbackDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionClawbackDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionClawbackRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var payeeId = await ResolvePayeeIdAsync(request.TenantId, request.PayeeId, cancellationToken);
        var transactionId = await ResolveTransactionIdAsync(request.TenantId, request.OriginalTransactionId, payeeId, cancellationToken);
        const string sql = @"
INSERT INTO Commission.CommissionClawback (ClawbackId, TenantId, PayeeId, OriginalTransactionId, ClawbackDate, Amount, ReasonCode, Notes, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PayeeId, @OriginalTransactionId, @ClawbackDate, @Amount, @ReasonCode, @Notes, @ApprovedByUserId, @ApprovedDateUtc, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, PayeeId = payeeId, OriginalTransactionId = transactionId, request.ClawbackDate, request.Amount, request.ReasonCode, request.Notes, request.ApprovedByUserId, request.ApprovedDateUtc, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionClawbackRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var payeeId = await ResolvePayeeIdAsync(request.TenantId, request.PayeeId, cancellationToken);
        var transactionId = await ResolveTransactionIdAsync(request.TenantId, request.OriginalTransactionId, payeeId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionClawback
SET PayeeId = @PayeeId,
    OriginalTransactionId = @OriginalTransactionId,
    ClawbackDate = @ClawbackDate,
    Amount = @Amount,
    ReasonCode = @ReasonCode,
    Notes = @Notes,
    ApprovedByUserId = @ApprovedByUserId,
    ApprovedDateUtc = @ApprovedDateUtc,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ClawbackId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, PayeeId = payeeId, OriginalTransactionId = transactionId, request.ClawbackDate, request.Amount, request.ReasonCode, request.Notes, request.ApprovedByUserId, request.ApprovedDateUtc, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
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
END;

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionTransaction (TransactionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, SourceEntityName NVARCHAR(100) NOT NULL, SourceEntityId UNIQUEIDENTIFIER NOT NULL, TransactionDate DATE NOT NULL, GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0, CommissionRate DECIMAL(9,4) NOT NULL DEFAULT 0, CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', PayoutId UNIQUEIDENTIFIER NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Commission.CommissionClawback', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionClawback (ClawbackId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, OriginalTransactionId UNIQUEIDENTIFIER NOT NULL, ClawbackDate DATE NOT NULL, Amount DECIMAL(18,2) NOT NULL, ReasonCode NVARCHAR(100) NOT NULL, Notes NVARCHAR(1000) NULL, ApprovedByUserId UNIQUEIDENTIFIER NULL, ApprovedDateUtc DATETIME2 NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ClawbackId') IS NULL ALTER TABLE Commission.CommissionClawback ADD ClawbackId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'TenantId') IS NULL ALTER TABLE Commission.CommissionClawback ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionClawback_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionClawback', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionClawback ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'OriginalTransactionId') IS NULL ALTER TABLE Commission.CommissionClawback ADD OriginalTransactionId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ClawbackDate') IS NULL ALTER TABLE Commission.CommissionClawback ADD ClawbackDate DATE NOT NULL CONSTRAINT DF_CommissionClawback_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionClawback', N'Amount') IS NULL ALTER TABLE Commission.CommissionClawback ADD Amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionClawback_Amount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ReasonCode') IS NULL ALTER TABLE Commission.CommissionClawback ADD ReasonCode NVARCHAR(100) NOT NULL CONSTRAINT DF_CommissionClawback_Reason DEFAULT N'Adjustment';
    IF COL_LENGTH(N'Commission.CommissionClawback', N'Notes') IS NULL ALTER TABLE Commission.CommissionClawback ADD Notes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ApprovedByUserId') IS NULL ALTER TABLE Commission.CommissionClawback ADD ApprovedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ApprovedDateUtc') IS NULL ALTER TABLE Commission.CommissionClawback ADD ApprovedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionClawback ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionClawback_Status DEFAULT N'Pending';
    IF COL_LENGTH(N'Commission.CommissionClawback', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionClawback ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionClawback_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionClawback', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionClawback ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionClawback ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'ModifiedByUserId') IS NULL ALTER TABLE Commission.CommissionClawback ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionClawback', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionClawback ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionClawback_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionClawback WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @SeedPayee UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        DECLARE @SeedTx UNIQUEIDENTIFIER = (SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        IF @SeedPayee IS NOT NULL AND @SeedTx IS NOT NULL
        BEGIN
            INSERT INTO Commission.CommissionClawback (ClawbackId, TenantId, PayeeId, OriginalTransactionId, ClawbackDate, Amount, ReasonCode, Notes, StatusCode, CreatedDateUtc, IsDeleted)
            VALUES (NEWID(), @SeedTenantId, @SeedPayee, @SeedTx, CONVERT(date, SYSUTCDATETIME()), 250, N''Policy Cancellation'', N''Tenant Admin seed clawback synchronized from commission transaction data.'', N''Pending'', SYSUTCDATETIME(), 0);
        END
    END', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task<Guid> ResolvePayeeIdAsync(Guid tenantId, Guid? payeeId, CancellationToken cancellationToken)
    {
        if (payeeId.HasValue && payeeId.Value != Guid.Empty) return payeeId.Value;
        const string sql = "SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task<Guid> ResolveTransactionIdAsync(Guid tenantId, Guid? transactionId, Guid payeeId, CancellationToken cancellationToken)
    {
        if (transactionId.HasValue && transactionId.Value != Guid.Empty) return transactionId.Value;
        const string sql = "SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @TenantId AND PayeeId = @PayeeId AND IsDeleted = 0 ORDER BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { TenantId = tenantId, PayeeId = payeeId }, cancellationToken: cancellationToken));
    }
}
