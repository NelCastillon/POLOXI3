using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionDisputeRepository : ICommissionDisputeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionDisputeRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionDisputeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = @"SELECT DisputeId, TenantId, PayeeId, TransactionId, DisputeDate, DisputeReason, DisputedAmount, Resolution, ResolvedByUserId, ResolvedDateUtc, StatusCode, CreatedDateUtc FROM Commission.CommissionDispute WHERE DisputeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionDisputeDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionDisputeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionDispute",
            "DisputeId, TenantId, PayeeId, TransactionId, DisputeDate, DisputeReason, DisputedAmount, Resolution, ResolvedByUserId, ResolvedDateUtc, StatusCode, CreatedDateUtc",
            "StatusCode LIKE '%' + @SearchTerm + '%' OR DisputeReason LIKE '%' + @SearchTerm + '%' OR Resolution LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<CommissionDisputeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionDisputeDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionDisputeRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        var payeeId = await ResolvePayeeIdAsync(request.TenantId, request.PayeeId, cancellationToken);
        var transactionId = await ResolveTransactionIdAsync(request.TenantId, request.TransactionId, payeeId, cancellationToken);
        const string sql = @"
INSERT INTO Commission.CommissionDispute (DisputeId, TenantId, PayeeId, TransactionId, DisputeDate, DisputeReason, DisputedAmount, Resolution, ResolvedByUserId, ResolvedDateUtc, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PayeeId, @TransactionId, @DisputeDate, @DisputeReason, @DisputedAmount, @Resolution, @ResolvedByUserId, @ResolvedDateUtc, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, PayeeId = payeeId, TransactionId = transactionId, request.DisputeDate, request.DisputeReason, request.DisputedAmount, request.Resolution, request.ResolvedByUserId, request.ResolvedDateUtc, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionDisputeRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var payeeId = await ResolvePayeeIdAsync(request.TenantId, request.PayeeId, cancellationToken);
        var transactionId = await ResolveTransactionIdAsync(request.TenantId, request.TransactionId, payeeId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionDispute
SET PayeeId = @PayeeId,
    TransactionId = @TransactionId,
    DisputeDate = @DisputeDate,
    DisputeReason = @DisputeReason,
    DisputedAmount = @DisputedAmount,
    Resolution = @Resolution,
    ResolvedByUserId = @ResolvedByUserId,
    ResolvedDateUtc = @ResolvedDateUtc,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE DisputeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, PayeeId = payeeId, TransactionId = transactionId, request.DisputeDate, request.DisputeReason, request.DisputedAmount, request.Resolution, request.ResolvedByUserId, request.ResolvedDateUtc, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
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
    IF COL_LENGTH(N'Commission.CommissionPayee', N'TenantId') IS NULL ALTER TABLE Commission.CommissionPayee ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionPayee_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeName') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeName NVARCHAR(255) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'UserId') IS NULL ALTER TABLE Commission.CommissionPayee ADD UserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionPayee ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeTypeCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD PayeeTypeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'SplitPercentage') IS NULL ALTER TABLE Commission.CommissionPayee ADD SplitPercentage DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionPayee_SplitPercentage DEFAULT 100;
    IF COL_LENGTH(N'Commission.CommissionPayee', N'EffectiveDate') IS NULL ALTER TABLE Commission.CommissionPayee ADD EffectiveDate DATE NOT NULL CONSTRAINT DF_CommissionPayee_EffectiveDate DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionPayee', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionPayee ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionPayee_Status DEFAULT N'Active';
    IF COL_LENGTH(N'Commission.CommissionPayee', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionPayee ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionPayee_IsDeleted DEFAULT 0;
END;

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionTransaction (TransactionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, CommissionPlanId UNIQUEIDENTIFIER NOT NULL, SourceEntityName NVARCHAR(100) NOT NULL, SourceEntityId UNIQUEIDENTIFIER NOT NULL, TransactionDate DATE NOT NULL, GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0, CommissionRate DECIMAL(9,4) NOT NULL DEFAULT 0, CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending', PayoutId UNIQUEIDENTIFIER NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'TenantId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTransaction_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionPlanId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'SourceEntityName') IS NULL ALTER TABLE Commission.CommissionTransaction ADD SourceEntityName NVARCHAR(100) NOT NULL CONSTRAINT DF_CommissionTransaction_SourceName DEFAULT N'Policy';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'SourceEntityId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD SourceEntityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionTransaction_SourceId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'GrossAmount') IS NULL ALTER TABLE Commission.CommissionTransaction ADD GrossAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionTransaction_Gross DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionRate') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionRate DECIMAL(9,4) NOT NULL CONSTRAINT DF_CommissionTransaction_Rate DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'CommissionAmount') IS NULL ALTER TABLE Commission.CommissionTransaction ADD CommissionAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionTransaction_Amount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'PayoutId') IS NULL ALTER TABLE Commission.CommissionTransaction ADD PayoutId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionTransaction', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionTransaction ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionTransaction_IsDeleted DEFAULT 0;
END;

IF OBJECT_ID(N'Commission.CommissionDispute', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionDispute (DisputeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, PayeeId UNIQUEIDENTIFIER NOT NULL, TransactionId UNIQUEIDENTIFIER NULL, DisputeDate DATE NOT NULL, DisputeReason NVARCHAR(500) NOT NULL, DisputedAmount DECIMAL(18,2) NOT NULL, Resolution NVARCHAR(1000) NULL, ResolvedByUserId UNIQUEIDENTIFIER NULL, ResolvedDateUtc DATETIME2 NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Open', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END
ELSE
BEGIN
    IF COL_LENGTH(N'Commission.CommissionDispute', N'DisputeId') IS NULL ALTER TABLE Commission.CommissionDispute ADD DisputeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'CommissionPayoutItemId') IS NULL ALTER TABLE Commission.CommissionDispute ADD CommissionPayoutItemId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'TenantId') IS NULL ALTER TABLE Commission.CommissionDispute ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommissionDispute_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Commission.CommissionDispute', N'PayeeId') IS NULL ALTER TABLE Commission.CommissionDispute ADD PayeeId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'TransactionId') IS NULL ALTER TABLE Commission.CommissionDispute ADD TransactionId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'DisputeDate') IS NULL ALTER TABLE Commission.CommissionDispute ADD DisputeDate DATE NOT NULL CONSTRAINT DF_CommissionDispute_Date DEFAULT CONVERT(date, SYSUTCDATETIME());
    IF COL_LENGTH(N'Commission.CommissionDispute', N'DisputeReason') IS NULL ALTER TABLE Commission.CommissionDispute ADD DisputeReason NVARCHAR(500) NOT NULL CONSTRAINT DF_CommissionDispute_Reason DEFAULT N'Commission exception';
    IF COL_LENGTH(N'Commission.CommissionDispute', N'DisputedAmount') IS NULL ALTER TABLE Commission.CommissionDispute ADD DisputedAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CommissionDispute_Amount DEFAULT 0;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'Resolution') IS NULL ALTER TABLE Commission.CommissionDispute ADD Resolution NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'ResolvedByUserId') IS NULL ALTER TABLE Commission.CommissionDispute ADD ResolvedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'ResolvedDateUtc') IS NULL ALTER TABLE Commission.CommissionDispute ADD ResolvedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'StatusCode') IS NULL ALTER TABLE Commission.CommissionDispute ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommissionDispute_Status DEFAULT N'Open';
    IF COL_LENGTH(N'Commission.CommissionDispute', N'StatusCodeId') IS NULL ALTER TABLE Commission.CommissionDispute ADD StatusCodeId INT NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'CreatedDateUtc') IS NULL ALTER TABLE Commission.CommissionDispute ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommissionDispute_Created DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Commission.CommissionDispute', N'CreatedByUserId') IS NULL ALTER TABLE Commission.CommissionDispute ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'ModifiedDateUtc') IS NULL ALTER TABLE Commission.CommissionDispute ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'ModifiedByUserId') IS NULL ALTER TABLE Commission.CommissionDispute ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Commission.CommissionDispute', N'IsDeleted') IS NULL ALTER TABLE Commission.CommissionDispute ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommissionDispute_IsDeleted DEFAULT 0;
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayee', N'PayeeCode') IS NOT NULL AND COL_LENGTH(N'Commission.CommissionPayee', N'PayeeName') IS NOT NULL AND COL_LENGTH(N'Commission.CommissionDispute', N'CommissionPayoutItemId') IS NOT NULL AND COL_LENGTH(N'Commission.CommissionDispute', N'StatusCodeId') IS NOT NULL
    BEGIN
        EXEC sp_executesql N'
    DECLARE @SeedPayee UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
    DECLARE @SeedPayoutItem UNIQUEIDENTIFIER = NULL;
    IF OBJECT_ID(N''Commission.CommissionPayoutItem'', N''U'') IS NOT NULL
        SELECT TOP 1 @SeedPayoutItem = CommissionPayoutItemId FROM Commission.CommissionPayoutItem ORDER BY CommissionPayoutItemId;

    IF @SeedPayee IS NULL AND COL_LENGTH(N''Commission.CommissionPayee'', N''CommissionPayeeTypeId'') IS NULL
    BEGIN
        DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        INSERT INTO Commission.CommissionPayee (PayeeId, TenantId, PayeeCode, PayeeName, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES (NEWID(), @SeedTenantId, CONCAT(N''PAY-'', LEFT(CONVERT(nvarchar(36), NEWID()), 8)), N''Demo Producer'', NULL, @PlanId, N''Producer'', 100, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0);
        SET @SeedPayee = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
    END;

    IF @SeedPayee IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        DECLARE @Plan UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        INSERT INTO Commission.CommissionTransaction (TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES (NEWID(), @SeedTenantId, @PayeeId, @Plan, N''Policy'', NEWID(), DATEADD(day, -30, CONVERT(date, SYSUTCDATETIME())), 12500, 10, 1250, N''Earned'', SYSUTCDATETIME(), 0);
    END;

    IF @SeedPayee IS NOT NULL AND @SeedPayoutItem IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Commission.CommissionDispute WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @SeedTx UNIQUEIDENTIFIER = (SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND PayeeId = @SeedPayee AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        INSERT INTO Commission.CommissionDispute (DisputeId, TenantId, PayeeId, CommissionPayoutItemId, TransactionId, DisputeDate, DisputeReason, DisputedAmount, Resolution, StatusCode, StatusCodeId, CreatedDateUtc, IsDeleted)
        VALUES
        (NEWID(), @SeedTenantId, @SeedPayee, @SeedPayoutItem, @SeedTx, DATEADD(day, -3, CONVERT(date, SYSUTCDATETIME())), N''Missing Payment - Statement commission not posted for current period'', 1250, NULL, N''Open'', 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SeedTenantId, @SeedPayee, @SeedPayoutItem, @SeedTx, DATEADD(day, -8, CONVERT(date, SYSUTCDATETIME())), N''Rate Mismatch - Applied renewal rate instead of new business rate'', 380, NULL, N''In Review'', 2, SYSUTCDATETIME(), 0),
        (NEWID(), @SeedTenantId, @SeedPayee, @SeedPayoutItem, @SeedTx, DATEADD(day, -30, CONVERT(date, SYSUTCDATETIME())), N''Calculation Error - Premium base corrected during review'', 210, N''Resolved after recalculation and producer notification.'', N''Resolved'', 3, SYSUTCDATETIME(), 0);
    END;', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
    END
    ELSE
    BEGIN
        EXEC sp_executesql N'
    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        INSERT INTO Commission.CommissionPayee (PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES (NEWID(), @SeedTenantId, NULL, @PlanId, N''Producer'', 100, CONVERT(date, SYSUTCDATETIME()), N''Active'', SYSUTCDATETIME(), 0);
    END;

    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        DECLARE @Plan UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        INSERT INTO Commission.CommissionTransaction (TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES (NEWID(), @SeedTenantId, @PayeeId, @Plan, N''Policy'', NEWID(), DATEADD(day, -30, CONVERT(date, SYSUTCDATETIME())), 12500, 10, 1250, N''Earned'', SYSUTCDATETIME(), 0);
    END;

    IF NOT EXISTS (SELECT 1 FROM Commission.CommissionDispute WHERE TenantId = @SeedTenantId AND IsDeleted = 0)
    BEGIN
        DECLARE @SeedPayee UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @SeedTenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        DECLARE @SeedTx UNIQUEIDENTIFIER = (SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @SeedTenantId AND PayeeId = @SeedPayee AND IsDeleted = 0 ORDER BY CreatedDateUtc);
        INSERT INTO Commission.CommissionDispute (DisputeId, TenantId, PayeeId, TransactionId, DisputeDate, DisputeReason, DisputedAmount, Resolution, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES
        (NEWID(), @SeedTenantId, @SeedPayee, @SeedTx, DATEADD(day, -3, CONVERT(date, SYSUTCDATETIME())), N''Missing Payment - Statement commission not posted for current period'', 1250, NULL, N''Open'', SYSUTCDATETIME(), 0),
        (NEWID(), @SeedTenantId, @SeedPayee, @SeedTx, DATEADD(day, -8, CONVERT(date, SYSUTCDATETIME())), N''Rate Mismatch - Applied renewal rate instead of new business rate'', 380, NULL, N''In Review'', SYSUTCDATETIME(), 0),
        (NEWID(), @SeedTenantId, @SeedPayee, @SeedTx, DATEADD(day, -30, CONVERT(date, SYSUTCDATETIME())), N''Calculation Error - Premium base corrected during review'', 210, N''Resolved after recalculation and producer notification.'', N''Resolved'', SYSUTCDATETIME(), 0);
    END;', N'@SeedTenantId UNIQUEIDENTIFIER', @SeedTenantId = @TenantId;
    END
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

    private async Task<Guid?> ResolveTransactionIdAsync(Guid tenantId, Guid? transactionId, Guid payeeId, CancellationToken cancellationToken)
    {
        if (transactionId.HasValue && transactionId.Value != Guid.Empty) return transactionId.Value;
        const string sql = "SELECT TOP 1 TransactionId FROM Commission.CommissionTransaction WHERE TenantId = @TenantId AND PayeeId = @PayeeId AND IsDeleted = 0 ORDER BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { TenantId = tenantId, PayeeId = payeeId }, cancellationToken: cancellationToken));
    }
}
