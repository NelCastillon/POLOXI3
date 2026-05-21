using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AuditLogRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<AuditLogDto>> SearchAsync(string? searchTerm, string? eventTypeCode, string? actor = null, string? entityName = null, string? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName,
           PerformedByUserId, OldValues, NewValues, IpAddress, RegionCode, CONVERT(NVARCHAR(120), CorrelationId) AS CorrelationId,
           PerformedDateUtc, CreatedDateUtc
    FROM Audit.AuditLog
    WHERE IsDeleted = 0
      AND (@EventTypeCode IS NULL OR @EventTypeCode = '' OR EventTypeCode = @EventTypeCode)
      AND (@EntityName IS NULL OR @EntityName = '' OR EntityName = @EntityName)
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@Actor IS NULL OR @Actor = '' OR CAST(PerformedByUserId AS NVARCHAR(36)) LIKE '%' + @Actor + '%')
      AND (@FromDate IS NULL OR PerformedDateUtc >= @FromDate)
      AND (@ToDate IS NULL OR PerformedDateUtc <= @ToDate)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR EntityName  LIKE '%' + @SearchTerm + '%'
           OR ActionName  LIKE '%' + @SearchTerm + '%'
           OR IpAddress   LIKE '%' + @SearchTerm + '%'
           OR CONVERT(NVARCHAR(120), CorrelationId) LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName,
           PerformedByUserId, OldValues, NewValues, IpAddress, RegionCode, CONVERT(NVARCHAR(120), CorrelationId) AS CorrelationId,
           PerformedDateUtc, CreatedDateUtc
    FROM Audit.AuditLog
    WHERE IsDeleted = 0
      AND (@EventTypeCode IS NULL OR @EventTypeCode = '' OR EventTypeCode = @EventTypeCode)
      AND (@EntityName IS NULL OR @EntityName = '' OR EntityName = @EntityName)
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@Actor IS NULL OR @Actor = '' OR CAST(PerformedByUserId AS NVARCHAR(36)) LIKE '%' + @Actor + '%')
      AND (@FromDate IS NULL OR PerformedDateUtc >= @FromDate)
      AND (@ToDate IS NULL OR PerformedDateUtc <= @ToDate)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR EntityName  LIKE '%' + @SearchTerm + '%'
           OR ActionName  LIKE '%' + @SearchTerm + '%'
           OR IpAddress   LIKE '%' + @SearchTerm + '%'
           OR CONVERT(NVARCHAR(120), CorrelationId) LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY PerformedDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        Guid? tenantGuid = Guid.TryParse(tenantId, out var tg) ? tg : null;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm = searchTerm,
            EventTypeCode = eventTypeCode,
            Actor = actor,
            EntityName = entityName,
            TenantId = tenantGuid,
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<AuditLogDto>()).ToList();
        return new PagedResult<AuditLogDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AuditLogDto?> GetByIdAsync(Guid auditLogId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName,
       PerformedByUserId, OldValues, NewValues, IpAddress, RegionCode, CONVERT(NVARCHAR(120), CorrelationId) AS CorrelationId,
       PerformedDateUtc, CreatedDateUtc
FROM Audit.AuditLog
WHERE AuditLogId = @AuditLogId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AuditLogDto>(sql, new { AuditLogId = auditLogId });
    }
}
