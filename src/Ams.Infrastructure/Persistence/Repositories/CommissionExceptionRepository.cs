using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionExceptionRepository : ICommissionExceptionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionExceptionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionExceptionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = SelectSql + @"
WHERE e.ExceptionId = @Id AND e.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionExceptionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionExceptionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? severityCode = null, string? typeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        const string sql = SelectSql + @"
WHERE e.TenantId = @TenantId
  AND e.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR e.ExceptionNumber LIKE N'%' + @SearchTerm + N'%' OR e.ExceptionTypeCode LIKE N'%' + @SearchTerm + N'%' OR e.Description LIKE N'%' + @SearchTerm + N'%' OR e.SourceCode LIKE N'%' + @SearchTerm + N'%' OR p.PayeeTypeCode LIKE N'%' + @SearchTerm + N'%' OR cp.PlanName LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR e.StatusCode = @StatusCode)
  AND (@SeverityCode IS NULL OR @SeverityCode = N'' OR e.SeverityCode = @SeverityCode)
  AND (@TypeCode IS NULL OR @TypeCode = N'' OR e.ExceptionTypeCode = @TypeCode)
ORDER BY
  CASE e.SeverityCode WHEN N'Critical' THEN 1 WHEN N'High' THEN 2 WHEN N'Medium' THEN 3 ELSE 4 END,
  CASE e.StatusCode WHEN N'Open' THEN 1 WHEN N'In Review' THEN 2 WHEN N'Deferred' THEN 3 WHEN N'Resolved' THEN 4 ELSE 5 END,
  e.CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Commission.CommissionException e
LEFT JOIN Commission.CommissionPayee p ON p.PayeeId = e.PayeeId
LEFT JOIN Commission.CommissionPlan cp ON cp.CommissionPlanId = e.CommissionPlanId
WHERE e.TenantId = @TenantId
  AND e.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR e.ExceptionNumber LIKE N'%' + @SearchTerm + N'%' OR e.ExceptionTypeCode LIKE N'%' + @SearchTerm + N'%' OR e.Description LIKE N'%' + @SearchTerm + N'%' OR e.SourceCode LIKE N'%' + @SearchTerm + N'%' OR p.PayeeTypeCode LIKE N'%' + @SearchTerm + N'%' OR cp.PlanName LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR e.StatusCode = @StatusCode)
  AND (@SeverityCode IS NULL OR @SeverityCode = N'' OR e.SeverityCode = @SeverityCode)
  AND (@TypeCode IS NULL OR @TypeCode = N'' OR e.ExceptionTypeCode = @TypeCode);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            StatusCode = statusCode,
            SeverityCode = severityCode,
            TypeCode = typeCode,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionExceptionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionExceptionDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionExceptionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Commission.CommissionException
(
    ExceptionId, TenantId, PayeeId, CommissionPlanId, TransactionId, PayoutBatchId, ExceptionNumber, ExceptionTypeCode,
    SeverityCode, SourceCode, Description, ImpactAmount, CurrencyCode, StatusCode, ResolutionNotes, AssignedToUserId,
    DueDateUtc, ResolvedByUserId, ResolvedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @Id, @TenantId, @PayeeId, @CommissionPlanId, @TransactionId, @PayoutBatchId, @ExceptionNumber, @ExceptionTypeCode,
    @SeverityCode, @SourceCode, @Description, @ImpactAmount, @CurrencyCode, @StatusCode, @ResolutionNotes, @AssignedToUserId,
    @DueDateUtc, @ResolvedByUserId, @ResolvedDateUtc, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PayeeId, request.CommissionPlanId, request.TransactionId, request.PayoutBatchId, request.ExceptionNumber, request.ExceptionTypeCode, request.SeverityCode, request.SourceCode, request.Description, request.ImpactAmount, request.CurrencyCode, request.StatusCode, request.ResolutionNotes, request.AssignedToUserId, request.DueDateUtc, request.ResolvedByUserId, request.ResolvedDateUtc, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionExceptionRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionException
SET PayeeId = @PayeeId,
    CommissionPlanId = @CommissionPlanId,
    TransactionId = @TransactionId,
    PayoutBatchId = @PayoutBatchId,
    ExceptionNumber = @ExceptionNumber,
    ExceptionTypeCode = @ExceptionTypeCode,
    SeverityCode = @SeverityCode,
    SourceCode = @SourceCode,
    Description = @Description,
    ImpactAmount = @ImpactAmount,
    CurrencyCode = @CurrencyCode,
    StatusCode = @StatusCode,
    ResolutionNotes = @ResolutionNotes,
    AssignedToUserId = @AssignedToUserId,
    DueDateUtc = @DueDateUtc,
    ResolvedByUserId = @ResolvedByUserId,
    ResolvedDateUtc = @ResolvedDateUtc,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ExceptionId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.PayeeId, request.CommissionPlanId, request.TransactionId, request.PayoutBatchId, request.ExceptionNumber, request.ExceptionTypeCode, request.SeverityCode, request.SourceCode, request.Description, request.ImpactAmount, request.CurrencyCode, request.StatusCode, request.ResolutionNotes, request.AssignedToUserId, request.DueDateUtc, request.ResolvedByUserId, request.ResolvedDateUtc, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionException', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionException
    (
        ExceptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        PayeeId UNIQUEIDENTIFIER NULL,
        CommissionPlanId UNIQUEIDENTIFIER NULL,
        TransactionId UNIQUEIDENTIFIER NULL,
        PayoutBatchId UNIQUEIDENTIFIER NULL,
        ExceptionNumber NVARCHAR(80) NOT NULL,
        ExceptionTypeCode NVARCHAR(80) NOT NULL,
        SeverityCode NVARCHAR(50) NOT NULL DEFAULT N'Medium',
        SourceCode NVARCHAR(80) NOT NULL DEFAULT N'Commission Run',
        Description NVARCHAR(1000) NOT NULL,
        ImpactAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CurrencyCode NVARCHAR(3) NOT NULL DEFAULT N'USD',
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Open',
        ResolutionNotes NVARCHAR(1000) NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        DueDateUtc DATETIME2 NULL,
        ResolvedByUserId UNIQUEIDENTIFIER NULL,
        ResolvedDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000' AND NOT EXISTS (SELECT 1 FROM Commission.CommissionException WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);
    DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);
    DECLARE @BatchId UNIQUEIDENTIFIER = (SELECT TOP 1 PayoutBatchId FROM Commission.CommissionPayoutBatch WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);

    INSERT INTO Commission.CommissionException (ExceptionId, TenantId, PayeeId, CommissionPlanId, PayoutBatchId, ExceptionNumber, ExceptionTypeCode, SeverityCode, SourceCode, Description, ImpactAmount, CurrencyCode, StatusCode, DueDateUtc, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, @PayeeId, @PlanId, @BatchId, CONCAT(N'CEX-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-001'), N'Missing Payee', N'Critical', N'Commission Run', N'A commission transaction was generated without a verified active payee assignment. Review producer setup before payout approval.', 3200.00, N'USD', N'Open', DATEADD(day, 1, SYSUTCDATETIME()), SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PayeeId, @PlanId, @BatchId, CONCAT(N'CEX-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-002'), N'Split Mismatch', N'High', N'Split Rules', N'Split allocations total 115% for a revenue event. Correct the rule stack and recalculate affected commissions.', 1850.00, N'USD', N'In Review', DATEADD(day, 2, SYSUTCDATETIME()), SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PayeeId, @PlanId, @BatchId, CONCAT(N'CEX-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-003'), N'Negative Payout', N'Medium', N'Payout Batch', N'Net payout dropped below zero after clawback application. Validate clawback timing and defer if needed.', 740.00, N'USD', N'Deferred', DATEADD(day, 5, SYSUTCDATETIME()), SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PayeeId, @PlanId, @BatchId, CONCAT(N'CEX-', FORMAT(DATEADD(month, -1, SYSUTCDATETIME()), 'yyyyMM'), N'-004'), N'Rate Variance', N'Low', N'Plan Audit', N'Commission rate varies from current plan by less than tolerance and has been documented for audit.', 125.00, N'USD', N'Resolved', DATEADD(day, -2, SYSUTCDATETIME()), SYSUTCDATETIME(), 0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private const string SelectSql = @"
SELECT e.ExceptionId,
       e.TenantId,
       e.PayeeId,
       COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(p.PayeeTypeCode, N' ', CONVERT(nvarchar(36), p.PayeeId)))), N''), N'Unassigned') AS PayeeName,
       e.CommissionPlanId,
       COALESCE(cp.PlanName, N'Unassigned plan') AS PlanName,
       e.TransactionId,
       e.PayoutBatchId,
       e.ExceptionNumber,
       e.ExceptionTypeCode,
       e.SeverityCode,
       e.SourceCode,
       e.Description,
       e.ImpactAmount,
       e.CurrencyCode,
       e.StatusCode,
       COALESCE(e.ResolutionNotes, N'') AS ResolutionNotes,
       e.AssignedToUserId,
       e.DueDateUtc,
       e.ResolvedByUserId,
       e.ResolvedDateUtc,
       e.CreatedDateUtc
FROM Commission.CommissionException e
LEFT JOIN Commission.CommissionPayee p ON p.PayeeId = e.PayeeId
LEFT JOIN Commission.CommissionPlan cp ON cp.CommissionPlanId = e.CommissionPlanId";
}
