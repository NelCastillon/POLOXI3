using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UsageRepository : IUsageRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UsageRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PlatformUsageDto> GetPlatformUsageAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
-- Platform totals
SELECT
    COUNT(1)                                         AS TotalTenants,
    SUM(CASE WHEN StatusCode = 'Active'     THEN 1 ELSE 0 END) AS ActiveTenants,
    SUM(CASE WHEN StatusCode = 'Suspended'  THEN 1 ELSE 0 END) AS SuspendedTenants,
    SUM(CASE WHEN StatusCode = 'Terminated' THEN 1 ELSE 0 END) AS TerminatedTenants,
    SUM(ISNULL(ActiveUsers, 0))                      AS TotalActiveUsers
FROM Core.Tenant
WHERE IsDeleted = 0;

-- Per-tenant rows (ordered by active users descending)
SELECT
    TenantId,
    TenantCode,
    TenantName,
    StatusCode,
    ISNULL(PlanCode, '')          AS PlanCode,
    ISNULL(ActiveUsers, 0)        AS ActiveUsers,
    CAST(0 AS DECIMAL(18,2))      AS StorageUsedGb,
    CAST(0 AS BIGINT)             AS ApiCallsToday,
    CAST(0 AS BIGINT)             AS JobsProcessed,
    CAST(0 AS BIGINT)             AS ExportsGenerated,
    CreatedDateUtc,
    ModifiedDateUtc               AS LastActivityDateUtc
FROM Core.Tenant
WHERE IsDeleted = 0
ORDER BY ActiveUsers DESC, TenantName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));

        var totals = await multi.ReadSingleAsync<TotalsRow>();
        var tenants = (await multi.ReadAsync<TenantUsageSummaryDto>()).AsList();

        return new PlatformUsageDto
        {
            TotalTenants          = totals.TotalTenants,
            ActiveTenants         = totals.ActiveTenants,
            SuspendedTenants      = totals.SuspendedTenants,
            TerminatedTenants     = totals.TerminatedTenants,
            TotalActiveUsers      = totals.TotalActiveUsers,
            TotalStorageUsedGb    = 0,
            TotalApiCallsToday    = 0,
            TotalJobsProcessed    = 0,
            TotalExportsGenerated = 0,
            SnapshotDateUtc       = DateTime.UtcNow,
            Tenants               = tenants
        };
    }

    public async Task<PagedResult<UsageEventDto>> GetUsageEventsAsync(
        Guid?   tenantId      = null,
        string? metricType    = null,
        string? sourceService = null,
        int     pageNumber    = 1,
        int     pageSize      = 50,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT
        e.EventId,
        e.EventTimeUtc,
        e.TenantId,
        ISNULL(t.TenantCode, '')  AS TenantCode,
        ISNULL(t.TenantName, '')  AS TenantName,
        e.MetricType,
        e.Quantity,
        e.SourceService,
        e.CorrelationId
    FROM Core.UsageEvent e
    LEFT JOIN Core.Tenant t ON t.TenantId = e.TenantId AND t.IsDeleted = 0
    WHERE (@TenantId      IS NULL OR e.TenantId      = @TenantId)
      AND (@MetricType    IS NULL OR e.MetricType    = @MetricType)
      AND (@SourceService IS NULL OR e.SourceService = @SourceService)
)
SELECT * FROM Cte
ORDER BY EventTimeUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Core.UsageEvent e
WHERE (@TenantId      IS NULL OR e.TenantId      = @TenantId)
  AND (@MetricType    IS NULL OR e.MetricType    = @MetricType)
  AND (@SourceService IS NULL OR e.SourceService = @SourceService);";

        var safePageNumber = Math.Max(pageNumber, 1);
        var safePageSize   = Math.Clamp(pageSize, 1, 500);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId      = tenantId,
            MetricType    = metricType,
            SourceService = sourceService,
            Offset        = (safePageNumber - 1) * safePageSize,
            PageSize      = safePageSize
        }, cancellationToken: cancellationToken));

        var items      = (await multi.ReadAsync<UsageEventDto>()).AsList();
        var totalCount = await multi.ReadSingleAsync<int>();

        return new PagedResult<UsageEventDto>
        {
            Items       = items,
            TotalCount  = totalCount,
            PageNumber  = safePageNumber,
            PageSize    = safePageSize
        };
    }

    private sealed record TotalsRow(
        int TotalTenants,
        int ActiveTenants,
        int SuspendedTenants,
        int TerminatedTenants,
        int TotalActiveUsers);
}
