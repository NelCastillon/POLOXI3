using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.QuotaRules;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class QuotaRuleRepository : IQuotaRuleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public QuotaRuleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<QuotaRuleDto>> SearchAsync(string? searchTerm, string? planCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT QuotaRuleId, RuleCode, PlanCode, MetricTypeCode, LimitValue, LimitUnit,
           PeriodCode, WarningThresholdPct, GraceThreshold, OverageBillingEnabled,
           EnforcementMode, IsEnforced, IsActive, Notes, CreatedDateUtc, ModifiedDateUtc
    FROM Core.QuotaRule
    WHERE IsDeleted = 0
      AND (@PlanCode    IS NULL OR @PlanCode    = '' OR PlanCode       = @PlanCode)
      AND (@IsActive    IS NULL                      OR IsActive        = @IsActive)
      AND (@SearchTerm  IS NULL OR @SearchTerm  = ''
           OR PlanCode        LIKE '%' + @SearchTerm + '%'
           OR MetricTypeCode  LIKE '%' + @SearchTerm + '%'
           OR RuleCode        LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT QuotaRuleId, RuleCode, PlanCode, MetricTypeCode, LimitValue, LimitUnit,
           PeriodCode, WarningThresholdPct, GraceThreshold, OverageBillingEnabled,
           EnforcementMode, IsEnforced, IsActive, Notes, CreatedDateUtc, ModifiedDateUtc
    FROM Core.QuotaRule
    WHERE IsDeleted = 0
      AND (@PlanCode    IS NULL OR @PlanCode    = '' OR PlanCode       = @PlanCode)
      AND (@IsActive    IS NULL                      OR IsActive        = @IsActive)
      AND (@SearchTerm  IS NULL OR @SearchTerm  = ''
           OR PlanCode        LIKE '%' + @SearchTerm + '%'
           OR MetricTypeCode  LIKE '%' + @SearchTerm + '%'
           OR RuleCode        LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY PlanCode, MetricTypeCode
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm = searchTerm,
            PlanCode   = planCode,
            IsActive   = isActive,
            PageNumber = pageNumber,
            PageSize   = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<QuotaRuleDto>()).ToList();
        return new PagedResult<QuotaRuleDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<QuotaRuleDto?> GetByIdAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT QuotaRuleId, RuleCode, PlanCode, MetricTypeCode, LimitValue, LimitUnit,
       PeriodCode, WarningThresholdPct, GraceThreshold, OverageBillingEnabled,
       EnforcementMode, IsEnforced, IsActive, Notes, CreatedDateUtc, ModifiedDateUtc
FROM Core.QuotaRule
WHERE QuotaRuleId = @QuotaRuleId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<QuotaRuleDto>(sql, new { QuotaRuleId = quotaRuleId });
    }

    public async Task<Guid> CreateAsync(CreateQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Core.QuotaRule
    (QuotaRuleId, RuleCode, PlanCode, MetricTypeCode, LimitValue, LimitUnit,
     PeriodCode, WarningThresholdPct, GraceThreshold, OverageBillingEnabled,
     EnforcementMode, IsEnforced, IsActive, Notes, CreatedDateUtc, CreatedByUserId)
VALUES
    (@NewId, @RuleCode, @PlanCode, @MetricTypeCode, @LimitValue, @LimitUnit,
     @PeriodCode, @WarningThresholdPct, @GraceThreshold, @OverageBillingEnabled,
     @EnforcementMode, @IsEnforced, 1, @Notes, SYSUTCDATETIME(), @CreatedByUserId);
SELECT @NewId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            request.RuleCode,
            request.PlanCode,
            request.MetricTypeCode,
            request.LimitValue,
            request.LimitUnit,
            request.PeriodCode,
            request.WarningThresholdPct,
            request.GraceThreshold,
            request.OverageBillingEnabled,
            request.EnforcementMode,
            request.IsEnforced,
            request.Notes,
            request.CreatedByUserId
        });
    }

    public async Task UpdateAsync(Guid quotaRuleId, UpdateQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaRule
SET RuleCode             = @RuleCode,
    PlanCode             = @PlanCode,
    MetricTypeCode       = @MetricTypeCode,
    LimitValue           = @LimitValue,
    LimitUnit            = @LimitUnit,
    PeriodCode           = @PeriodCode,
    WarningThresholdPct  = @WarningThresholdPct,
    GraceThreshold       = @GraceThreshold,
    OverageBillingEnabled = @OverageBillingEnabled,
    EnforcementMode      = @EnforcementMode,
    IsEnforced           = @IsEnforced,
    IsActive             = @IsActive,
    Notes                = @Notes,
    ModifiedDateUtc      = SYSUTCDATETIME()
WHERE QuotaRuleId = @QuotaRuleId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            QuotaRuleId = quotaRuleId,
            request.RuleCode,
            request.PlanCode,
            request.MetricTypeCode,
            request.LimitValue,
            request.LimitUnit,
            request.PeriodCode,
            request.WarningThresholdPct,
            request.GraceThreshold,
            request.OverageBillingEnabled,
            request.EnforcementMode,
            request.IsEnforced,
            request.IsActive,
            request.Notes
        });
    }

    public async Task DeleteAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.QuotaRule SET IsDeleted = 1 WHERE QuotaRuleId = @QuotaRuleId;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { QuotaRuleId = quotaRuleId });
    }

    public async Task<Guid> CloneAsync(Guid quotaRuleId, CloneQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Core.QuotaRule
    (QuotaRuleId, RuleCode, PlanCode, MetricTypeCode, LimitValue, LimitUnit,
     PeriodCode, WarningThresholdPct, GraceThreshold, OverageBillingEnabled,
     EnforcementMode, IsEnforced, IsActive, Notes, CreatedDateUtc, CreatedByUserId)
SELECT @NewId, @NewRuleCode, PlanCode, MetricTypeCode, LimitValue, LimitUnit,
       PeriodCode, WarningThresholdPct, GraceThreshold, OverageBillingEnabled,
       EnforcementMode, IsEnforced, 1, Notes, SYSUTCDATETIME(), @CreatedByUserId
FROM Core.QuotaRule
WHERE QuotaRuleId = @QuotaRuleId AND IsDeleted = 0;
SELECT @NewId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            QuotaRuleId = quotaRuleId,
            request.NewRuleCode,
            request.CreatedByUserId
        });
    }

    public async Task ActivateAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.QuotaRule SET IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE QuotaRuleId = @QuotaRuleId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { QuotaRuleId = quotaRuleId });
    }

    public async Task DeactivateAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.QuotaRule SET IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME() WHERE QuotaRuleId = @QuotaRuleId AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { QuotaRuleId = quotaRuleId });
    }
}
