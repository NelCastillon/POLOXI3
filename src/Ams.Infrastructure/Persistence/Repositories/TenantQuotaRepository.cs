using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.TenantQuotas;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantQuotaRepository : ITenantQuotaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TenantQuotaRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<TenantQuotaDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT q.TenantQuotaId, q.TenantId, ISNULL(t.TenantName, '') AS TenantName,
           q.MetricTypeCode, q.LimitValue, q.CurrentValue, q.LimitUnit,
           q.PeriodCode, q.IsEnforced, q.StatusCode, q.OverrideReason,
           q.LastResetDateUtc, q.NextResetDateUtc, q.CreatedDateUtc, q.ModifiedDateUtc
    FROM Core.TenantQuota q
    LEFT JOIN Core.Tenant t ON t.TenantId = q.TenantId
    WHERE q.IsDeleted = 0
      AND (@StatusCode  IS NULL OR @StatusCode  = '' OR q.StatusCode = @StatusCode)
      AND (@SearchTerm  IS NULL OR @SearchTerm  = ''
           OR t.TenantName      LIKE '%' + @SearchTerm + '%'
           OR q.MetricTypeCode  LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT q.TenantQuotaId, q.TenantId, ISNULL(t.TenantName, '') AS TenantName,
           q.MetricTypeCode, q.LimitValue, q.CurrentValue, q.LimitUnit,
           q.PeriodCode, q.IsEnforced, q.StatusCode, q.OverrideReason,
           q.LastResetDateUtc, q.NextResetDateUtc, q.CreatedDateUtc, q.ModifiedDateUtc
    FROM Core.TenantQuota q
    LEFT JOIN Core.Tenant t ON t.TenantId = q.TenantId
    WHERE q.IsDeleted = 0
      AND (@StatusCode  IS NULL OR @StatusCode  = '' OR q.StatusCode = @StatusCode)
      AND (@SearchTerm  IS NULL OR @SearchTerm  = ''
           OR t.TenantName      LIKE '%' + @SearchTerm + '%'
           OR q.MetricTypeCode  LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY TenantName, MetricTypeCode
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm = searchTerm,
            StatusCode = statusCode,
            PageNumber = pageNumber,
            PageSize   = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<TenantQuotaDto>()).ToList();
        return new PagedResult<TenantQuotaDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<TenantQuotaDto?> GetByTenantMetricAsync(Guid tenantId, string metricTypeCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.TenantQuotaId, q.TenantId, ISNULL(t.TenantName, '') AS TenantName,
       q.MetricTypeCode, q.LimitValue, q.CurrentValue, q.LimitUnit,
       q.PeriodCode, q.IsEnforced, q.StatusCode, q.OverrideReason,
       q.LastResetDateUtc, q.NextResetDateUtc, q.CreatedDateUtc, q.ModifiedDateUtc
FROM Core.TenantQuota q
LEFT JOIN Core.Tenant t ON t.TenantId = q.TenantId
WHERE q.TenantId = @TenantId AND q.MetricTypeCode = @MetricTypeCode AND q.IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantQuotaDto>(sql, new { TenantId = tenantId, MetricTypeCode = metricTypeCode });
    }

    public async Task<IReadOnlyList<TenantQuotaDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.TenantQuotaId, q.TenantId, ISNULL(t.TenantName, '') AS TenantName,
       q.MetricTypeCode, q.LimitValue, q.CurrentValue, q.LimitUnit,
       q.PeriodCode, q.IsEnforced, q.StatusCode, q.OverrideReason,
       q.LastResetDateUtc, q.NextResetDateUtc, q.CreatedDateUtc, q.ModifiedDateUtc
FROM Core.TenantQuota q
LEFT JOIN Core.Tenant t ON t.TenantId = q.TenantId
WHERE q.TenantId = @TenantId AND q.IsDeleted = 0
ORDER BY q.MetricTypeCode;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<TenantQuotaDto>(sql, new { TenantId = tenantId })).ToList();
    }

    public async Task<Guid> UpsertAsync(Guid tenantId, UpsertTenantQuotaRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER =
    (SELECT TOP 1 TenantQuotaId FROM Core.TenantQuota WHERE TenantId = @TenantId AND MetricTypeCode = @MetricTypeCode AND IsDeleted = 0);

IF @ExistingId IS NULL
BEGIN
    SET @ExistingId = NEWID();
    INSERT INTO Core.TenantQuota
        (TenantQuotaId, TenantId, MetricTypeCode, LimitValue, LimitUnit,
         PeriodCode, IsEnforced, StatusCode, OverrideReason, CreatedDateUtc, CreatedByUserId)
    VALUES
        (@ExistingId, @TenantId, @MetricTypeCode, @LimitValue, @LimitUnit,
         @PeriodCode, @IsEnforced, @StatusCode, @OverrideReason, SYSUTCDATETIME(), @CreatedByUserId);
END
ELSE
BEGIN
    UPDATE Core.TenantQuota
    SET LimitValue      = @LimitValue,
        LimitUnit       = @LimitUnit,
        PeriodCode      = @PeriodCode,
        IsEnforced      = @IsEnforced,
        StatusCode      = @StatusCode,
        OverrideReason  = @OverrideReason,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantQuotaId = @ExistingId;
END

SELECT @ExistingId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            TenantId = tenantId,
            request.MetricTypeCode,
            request.LimitValue,
            request.LimitUnit,
            request.PeriodCode,
            request.IsEnforced,
            request.StatusCode,
            request.OverrideReason,
            request.CreatedByUserId
        });
    }

    public async Task DeleteAsync(Guid tenantQuotaId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.TenantQuota SET IsDeleted = 1 WHERE TenantQuotaId = @TenantQuotaId;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantQuotaId = tenantQuotaId });
    }

    public async Task OverrideLimitAsync(Guid tenantQuotaId, OverrideLimitRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.TenantQuota
SET LimitValue      = @NewLimitValue,
    OverrideReason  = @OverrideReason,
    ModifiedDateUtc = SYSUTCDATETIME(),
    CreatedByUserId = COALESCE(@ModifiedByUserId, CreatedByUserId)
WHERE TenantQuotaId = @TenantQuotaId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantQuotaId = tenantQuotaId,
            request.NewLimitValue,
            request.OverrideReason,
            request.ModifiedByUserId
        });
    }

    public async Task ResetOverrideAsync(Guid tenantQuotaId, ResetOverrideRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE q
SET q.LimitValue      = COALESCE(r.LimitValue, q.LimitValue),
    q.OverrideReason   = NULL,
    q.LastResetDateUtc = SYSUTCDATETIME(),
    q.ModifiedDateUtc  = SYSUTCDATETIME()
FROM Core.TenantQuota q
LEFT JOIN Core.QuotaRule r ON r.MetricTypeCode = q.MetricTypeCode
    AND r.PlanCode = (SELECT t.PlanCode FROM Core.Tenant t WHERE t.TenantId = q.TenantId)
    AND r.IsDeleted = 0 AND r.IsActive = 1
WHERE q.TenantQuotaId = @TenantQuotaId AND q.IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantQuotaId = tenantQuotaId });
    }

    public async Task NotifyTenantAsync(Guid tenantQuotaId, NotifyTenantQuotaRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.TenantQuota
SET StatusCode      = CASE WHEN StatusCode = 'Active' THEN 'Active' ELSE StatusCode END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantQuotaId = @TenantQuotaId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantQuotaId = tenantQuotaId });
    }
}
