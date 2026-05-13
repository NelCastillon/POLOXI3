using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPayoutBatchRepository : ICommissionPayoutBatchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionPayoutBatchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionPayoutBatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = @"SELECT PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, ProcessedByUserId, ProcessedDateUtc, CreatedDateUtc FROM Commission.CommissionPayoutBatch WHERE PayoutBatchId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPayoutBatchDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPayoutBatchDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionPayoutBatch",
            "PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, ProcessedByUserId, ProcessedDateUtc, CreatedDateUtc",
            "BatchReference LIKE '%' + @SearchTerm + '%'",
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

        var items = (await multi.ReadAsync<CommissionPayoutBatchDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionPayoutBatchDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionPayoutBatchRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchNumber') IS NOT NULL
BEGIN
    INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, ProcessedByUserId, ProcessedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@Id, @TenantId, @BatchReference, @BatchReference, @PayPeriodStart, @PayPeriodEnd, @TotalAmount, @PayoutCount, @StatusCode, @ProcessedByUserId, @ProcessedDateUtc, SYSUTCDATETIME(), @CreatedByUserId, 0);
END
ELSE
BEGIN
    INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, ProcessedByUserId, ProcessedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@Id, @TenantId, @BatchReference, @PayPeriodStart, @PayPeriodEnd, @TotalAmount, @PayoutCount, @StatusCode, @ProcessedByUserId, @ProcessedDateUtc, SYSUTCDATETIME(), @CreatedByUserId, 0);
END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.BatchReference, request.PayPeriodStart, request.PayPeriodEnd, request.TotalAmount, request.PayoutCount, request.StatusCode, request.ProcessedByUserId, request.ProcessedDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionPayoutBatchRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        const string sql = @"
IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchNumber') IS NOT NULL
BEGIN
    UPDATE Commission.CommissionPayoutBatch
    SET BatchNumber = @BatchReference,
        BatchReference = @BatchReference,
        PayPeriodStart = @PayPeriodStart,
        PayPeriodEnd = @PayPeriodEnd,
        TotalAmount = @TotalAmount,
        PayoutCount = @PayoutCount,
        StatusCode = @StatusCode,
        ProcessedByUserId = @ProcessedByUserId,
        ProcessedDateUtc = @ProcessedDateUtc,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ModifiedByUserId
    WHERE PayoutBatchId = @Id AND IsDeleted = 0;
END
ELSE
BEGIN
    UPDATE Commission.CommissionPayoutBatch
    SET BatchReference = @BatchReference,
    PayPeriodStart = @PayPeriodStart,
    PayPeriodEnd = @PayPeriodEnd,
    TotalAmount = @TotalAmount,
    PayoutCount = @PayoutCount,
    StatusCode = @StatusCode,
    ProcessedByUserId = @ProcessedByUserId,
    ProcessedDateUtc = @ProcessedDateUtc,
    ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ModifiedByUserId
    WHERE PayoutBatchId = @Id AND IsDeleted = 0;
END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.BatchReference, request.PayPeriodStart, request.PayPeriodEnd, request.TotalAmount, request.PayoutCount, request.StatusCode, request.ProcessedByUserId, request.ProcessedDateUtc, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionPayoutBatch', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionPayoutBatch
    (
        PayoutBatchId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        BatchReference NVARCHAR(80) NOT NULL,
        PayPeriodStart DATE NOT NULL,
        PayPeriodEnd DATE NOT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        PayoutCount INT NOT NULL DEFAULT 0,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        ProcessedByUserId UNIQUEIDENTIFIER NULL,
        ProcessedDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000' AND NOT EXISTS (SELECT 1 FROM Commission.CommissionPayoutBatch WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    IF COL_LENGTH(N'Commission.CommissionPayoutBatch', N'BatchNumber') IS NOT NULL
    BEGIN
        INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchNumber, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES
        (NEWID(), @TenantId, CONCAT(N'PAY-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-001'), CONCAT(N'PAY-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-001'), DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N'Draft', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, CONCAT(N'PAY-', FORMAT(DATEADD(month, -1, SYSUTCDATETIME()), 'yyyyMM'), N'-001'), CONCAT(N'PAY-', FORMAT(DATEADD(month, -1, SYSUTCDATETIME()), 'yyyyMM'), N'-001'), DATEADD(day, -45, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, -31, CONVERT(date, SYSUTCDATETIME())), 24250, 12, N'Processed', SYSUTCDATETIME(), 0);
    END
    ELSE
    BEGIN
        INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode, CreatedDateUtc, IsDeleted)
        VALUES
        (NEWID(), @TenantId, CONCAT(N'PAY-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-001'), DATEADD(day, -14, CONVERT(date, SYSUTCDATETIME())), CONVERT(date, SYSUTCDATETIME()), 18500, 8, N'Draft', SYSUTCDATETIME(), 0),
        (NEWID(), @TenantId, CONCAT(N'PAY-', FORMAT(DATEADD(month, -1, SYSUTCDATETIME()), 'yyyyMM'), N'-001'), DATEADD(day, -45, CONVERT(date, SYSUTCDATETIME())), DATEADD(day, -31, CONVERT(date, SYSUTCDATETIME())), 24250, 12, N'Processed', SYSUTCDATETIME(), 0);
    END
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
