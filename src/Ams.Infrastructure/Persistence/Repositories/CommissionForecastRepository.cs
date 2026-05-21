using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionForecastRepository : ICommissionForecastRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionForecastRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionForecastDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(null, cancellationToken);
        const string sql = SelectSql + @"
WHERE f.ForecastId = @Id AND f.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionForecastDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionForecastDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? scenarioCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(tenantId, cancellationToken);
        const string sql = SelectSql + @"
WHERE f.TenantId = @TenantId
  AND f.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR f.ForecastNumber LIKE N'%' + @SearchTerm + N'%' OR f.ForecastName LIKE N'%' + @SearchTerm + N'%' OR f.ScenarioCode LIKE N'%' + @SearchTerm + N'%' OR f.StatusCode LIKE N'%' + @SearchTerm + N'%' OR f.Notes LIKE N'%' + @SearchTerm + N'%' OR p.PayeeTypeCode LIKE N'%' + @SearchTerm + N'%' OR cp.PlanName LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR f.StatusCode = @StatusCode)
  AND (@ScenarioCode IS NULL OR @ScenarioCode = N'' OR f.ScenarioCode = @ScenarioCode)
ORDER BY f.PeriodStart DESC, f.CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Commission.CommissionForecast f
LEFT JOIN Commission.CommissionPayee p ON p.PayeeId = f.PayeeId
LEFT JOIN Commission.CommissionPlan cp ON cp.CommissionPlanId = f.CommissionPlanId
WHERE f.TenantId = @TenantId
  AND f.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR f.ForecastNumber LIKE N'%' + @SearchTerm + N'%' OR f.ForecastName LIKE N'%' + @SearchTerm + N'%' OR f.ScenarioCode LIKE N'%' + @SearchTerm + N'%' OR f.StatusCode LIKE N'%' + @SearchTerm + N'%' OR f.Notes LIKE N'%' + @SearchTerm + N'%' OR p.PayeeTypeCode LIKE N'%' + @SearchTerm + N'%' OR cp.PlanName LIKE N'%' + @SearchTerm + N'%')
  AND (@StatusCode IS NULL OR @StatusCode = N'' OR f.StatusCode = @StatusCode)
  AND (@ScenarioCode IS NULL OR @ScenarioCode = N'' OR f.ScenarioCode = @ScenarioCode);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            StatusCode = statusCode,
            ScenarioCode = scenarioCode,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionForecastDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionForecastDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCommissionForecastRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Commission.CommissionForecast
(
    ForecastId, TenantId, CommissionPlanId, PayeeId, ForecastNumber, ForecastName, PeriodStart, PeriodEnd,
    PipelinePremium, WeightedPremium, ExpectedRevenue, ForecastCommission, ConfidencePct, ActualCommission,
    ScenarioCode, StatusCode, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @Id, @TenantId, @CommissionPlanId, @PayeeId, @ForecastNumber, @ForecastName, @PeriodStart, @PeriodEnd,
    @PipelinePremium, @WeightedPremium, @ExpectedRevenue, @ForecastCommission, @ConfidencePct, @ActualCommission,
    @ScenarioCode, @StatusCode, @Notes, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CommissionPlanId, request.PayeeId, request.ForecastNumber, request.ForecastName, request.PeriodStart, request.PeriodEnd, request.PipelinePremium, request.WeightedPremium, request.ExpectedRevenue, request.ForecastCommission, request.ConfidencePct, request.ActualCommission, request.ScenarioCode, request.StatusCode, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCommissionForecastRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAndSeedAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Commission.CommissionForecast
SET CommissionPlanId = @CommissionPlanId,
    PayeeId = @PayeeId,
    ForecastNumber = @ForecastNumber,
    ForecastName = @ForecastName,
    PeriodStart = @PeriodStart,
    PeriodEnd = @PeriodEnd,
    PipelinePremium = @PipelinePremium,
    WeightedPremium = @WeightedPremium,
    ExpectedRevenue = @ExpectedRevenue,
    ForecastCommission = @ForecastCommission,
    ConfidencePct = @ConfidencePct,
    ActualCommission = @ActualCommission,
    ScenarioCode = @ScenarioCode,
    StatusCode = @StatusCode,
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ForecastId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CommissionPlanId, request.PayeeId, request.ForecastNumber, request.ForecastName, request.PeriodStart, request.PeriodEnd, request.PipelinePremium, request.WeightedPremium, request.ExpectedRevenue, request.ForecastCommission, request.ConfidencePct, request.ActualCommission, request.ScenarioCode, request.StatusCode, request.Notes, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => EnsureSchemaAndSeedAsync(tenantId, cancellationToken);

    private async Task EnsureSchemaAndSeedAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Commission') EXEC(N'CREATE SCHEMA Commission');

IF OBJECT_ID(N'Commission.CommissionForecast', N'U') IS NULL
BEGIN
    CREATE TABLE Commission.CommissionForecast
    (
        ForecastId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CommissionPlanId UNIQUEIDENTIFIER NULL,
        PayeeId UNIQUEIDENTIFIER NULL,
        ForecastNumber NVARCHAR(80) NOT NULL,
        ForecastName NVARCHAR(200) NOT NULL,
        PeriodStart DATE NOT NULL,
        PeriodEnd DATE NOT NULL,
        PipelinePremium DECIMAL(18,2) NOT NULL DEFAULT 0,
        WeightedPremium DECIMAL(18,2) NOT NULL DEFAULT 0,
        ExpectedRevenue DECIMAL(18,2) NOT NULL DEFAULT 0,
        ForecastCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
        ConfidencePct DECIMAL(9,2) NOT NULL DEFAULT 75,
        ActualCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
        ScenarioCode NVARCHAR(50) NOT NULL DEFAULT N'Base',
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        Notes NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF @TenantId IS NOT NULL AND @TenantId <> '00000000-0000-0000-0000-000000000000' AND NOT EXISTS (SELECT 1 FROM Commission.CommissionForecast WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    DECLARE @PlanId UNIQUEIDENTIFIER = (SELECT TOP 1 CommissionPlanId FROM Commission.CommissionPlan WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);
    DECLARE @PayeeId UNIQUEIDENTIFIER = (SELECT TOP 1 PayeeId FROM Commission.CommissionPayee WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);

    INSERT INTO Commission.CommissionForecast (ForecastId, TenantId, CommissionPlanId, PayeeId, ForecastNumber, ForecastName, PeriodStart, PeriodEnd, PipelinePremium, WeightedPremium, ExpectedRevenue, ForecastCommission, ConfidencePct, ActualCommission, ScenarioCode, StatusCode, Notes, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'FC-', FORMAT(SYSUTCDATETIME(), 'yyyyMM'), N'-BASE'), N'Current Month Base Forecast', DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1), EOMONTH(SYSUTCDATETIME()), 620000, 410000, 61500, 18450, 78, 0, N'Base', N'Active', N'Weighted by current pipeline, renewal likelihood, and active split rules.', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'FC-', FORMAT(DATEADD(month, 1, SYSUTCDATETIME()), 'yyyyMM'), N'-UPSIDE'), N'Next Month Upside Forecast', DATEFROMPARTS(YEAR(DATEADD(month, 1, SYSUTCDATETIME())), MONTH(DATEADD(month, 1, SYSUTCDATETIME())), 1), EOMONTH(DATEADD(month, 1, SYSUTCDATETIME())), 815000, 560000, 84000, 25200, 68, 0, N'Upside', N'Draft', N'Upside scenario assumes improved bind ratio on commercial submissions.', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'FC-', FORMAT(DATEADD(month, -1, SYSUTCDATETIME()), 'yyyyMM'), N'-ACTUAL'), N'Prior Month Actualized Forecast', DATEFROMPARTS(YEAR(DATEADD(month, -1, SYSUTCDATETIME())), MONTH(DATEADD(month, -1, SYSUTCDATETIME())), 1), EOMONTH(DATEADD(month, -1, SYSUTCDATETIME())), 585000, 398000, 59700, 17910, 82, 17125, N'Base', N'Closed', N'Closed forecast reconciled against issued commission statements.', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, @PlanId, @PayeeId, CONCAT(N'FC-', FORMAT(DATEADD(month, 2, SYSUTCDATETIME()), 'yyyyMM'), N'-RISK'), N'Forward Risk Forecast', DATEFROMPARTS(YEAR(DATEADD(month, 2, SYSUTCDATETIME())), MONTH(DATEADD(month, 2, SYSUTCDATETIME())), 1), EOMONTH(DATEADD(month, 2, SYSUTCDATETIME())), 470000, 275000, 41250, 12375, 54, 0, N'Downside', N'Draft', N'Downside scenario reflects renewal risk and unresolved payout exceptions.', SYSUTCDATETIME(), 0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private const string SelectSql = @"
SELECT f.ForecastId,
       f.TenantId,
       f.CommissionPlanId,
       COALESCE(cp.PlanName, N'Unassigned plan') AS PlanName,
       f.PayeeId,
       COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(p.PayeeTypeCode, N' ', CONVERT(nvarchar(36), p.PayeeId)))), N''), N'All payees') AS PayeeName,
       f.ForecastNumber,
       f.ForecastName,
       f.PeriodStart,
       f.PeriodEnd,
       f.PipelinePremium,
       f.WeightedPremium,
       f.ExpectedRevenue,
       f.ForecastCommission,
       f.ConfidencePct,
       f.ActualCommission,
       f.ForecastCommission - f.ActualCommission AS VarianceAmount,
       f.ScenarioCode,
       f.StatusCode,
       COALESCE(f.Notes, N'') AS Notes,
       f.CreatedDateUtc
FROM Commission.CommissionForecast f
LEFT JOIN Commission.CommissionPayee p ON p.PayeeId = f.PayeeId
LEFT JOIN Commission.CommissionPlan cp ON cp.CommissionPlanId = f.CommissionPlanId";
}
