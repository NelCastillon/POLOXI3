using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Alerts;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AlertRepository : IAlertRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AlertRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<AlertDto>> SearchAsync(string? searchTerm, string? statusCode, string? severityCode, string? regionCode = null, Guid? tenantId = null, bool? openOnly = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT AlertId, AlertName, AlertTypeCode, ServiceName, SeverityCode, StatusCode, RegionCode, TenantId, OwnerUserId,
           Message, TriggeredDateUtc, AcknowledgedByUserId, AcknowledgedDateUtc,
           ResolvedByUserId, ResolvedDateUtc, EscalatedDateUtc, Notes, CreatedDateUtc
    FROM Core.Alert
    WHERE IsDeleted = 0
      AND (@StatusCode   IS NULL OR @StatusCode   = '' OR StatusCode   = @StatusCode)
      AND (@SeverityCode IS NULL OR @SeverityCode = '' OR SeverityCode = @SeverityCode)
      AND (@RegionCode   IS NULL OR @RegionCode   = '' OR RegionCode   = @RegionCode)
      AND (@TenantId     IS NULL OR TenantId = @TenantId)
      AND (@OpenOnly     IS NULL OR @OpenOnly = 0 OR StatusCode = 'Open')
      AND (@SearchTerm   IS NULL OR @SearchTerm   = ''
           OR AlertName   LIKE '%' + @SearchTerm + '%'
           OR ServiceName LIKE '%' + @SearchTerm + '%'
           OR Message     LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT AlertId, AlertName, AlertTypeCode, ServiceName, SeverityCode, StatusCode, RegionCode, TenantId, OwnerUserId,
           Message, TriggeredDateUtc, AcknowledgedByUserId, AcknowledgedDateUtc,
           ResolvedByUserId, ResolvedDateUtc, EscalatedDateUtc, Notes, CreatedDateUtc
    FROM Core.Alert
    WHERE IsDeleted = 0
      AND (@StatusCode   IS NULL OR @StatusCode   = '' OR StatusCode   = @StatusCode)
      AND (@SeverityCode IS NULL OR @SeverityCode = '' OR SeverityCode = @SeverityCode)
      AND (@RegionCode   IS NULL OR @RegionCode   = '' OR RegionCode   = @RegionCode)
      AND (@TenantId     IS NULL OR TenantId = @TenantId)
      AND (@OpenOnly     IS NULL OR @OpenOnly = 0 OR StatusCode = 'Open')
      AND (@SearchTerm   IS NULL OR @SearchTerm   = ''
           OR AlertName   LIKE '%' + @SearchTerm + '%'
           OR ServiceName LIKE '%' + @SearchTerm + '%'
           OR Message     LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY TriggeredDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm   = searchTerm,
            StatusCode   = statusCode,
            SeverityCode = severityCode,
            RegionCode   = regionCode,
            TenantId     = tenantId,
            OpenOnly     = openOnly,
            PageNumber   = pageNumber,
            PageSize     = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<AlertDto>()).ToList();
        return new PagedResult<AlertDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<AlertDto?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT AlertId, AlertName, AlertTypeCode, ServiceName, SeverityCode, StatusCode, RegionCode, TenantId, OwnerUserId,
       Message, TriggeredDateUtc, AcknowledgedByUserId, AcknowledgedDateUtc,
       ResolvedByUserId, ResolvedDateUtc, EscalatedDateUtc, Notes, CreatedDateUtc
FROM Core.Alert
WHERE AlertId = @AlertId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AlertDto>(sql, new { AlertId = alertId });
    }

    public async Task AcknowledgeAsync(Guid alertId, AcknowledgeAlertRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Alert
SET StatusCode           = 'Acknowledged',
    AcknowledgedByUserId = @AcknowledgedByUserId,
    AcknowledgedDateUtc  = SYSUTCDATETIME(),
    Notes                = COALESCE(@Notes, Notes)
WHERE AlertId = @AlertId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AlertId = alertId, request.AcknowledgedByUserId, request.Notes });
    }

    public async Task ResolveAsync(Guid alertId, ResolveAlertRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Alert
SET StatusCode        = 'Resolved',
    ResolvedByUserId  = @ResolvedByUserId,
    ResolvedDateUtc   = SYSUTCDATETIME(),
    Notes             = COALESCE(@Notes, Notes)
WHERE AlertId = @AlertId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AlertId = alertId, request.ResolvedByUserId, request.Notes });
    }

    public async Task AssignAsync(Guid alertId, AssignAlertRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Alert
SET OwnerUserId = @OwnerUserId,
    Notes       = COALESCE(@Notes, Notes)
WHERE AlertId = @AlertId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AlertId = alertId, request.OwnerUserId, request.Notes });
    }

    public async Task EscalateAsync(Guid alertId, EscalateAlertRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Alert
SET SeverityCode     = COALESCE(@SeverityCode, SeverityCode),
    EscalatedDateUtc = SYSUTCDATETIME(),
    Notes            = COALESCE(@Notes, Notes)
WHERE AlertId = @AlertId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { AlertId = alertId, request.SeverityCode, request.Notes });
    }

    public async Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM Core.Alert WHERE StatusCode = 'Open' AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(sql);
    }
}
