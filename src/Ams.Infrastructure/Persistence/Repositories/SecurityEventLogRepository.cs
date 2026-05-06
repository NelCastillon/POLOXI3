using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SecurityEventLogRepository : ISecurityEventLogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SecurityEventLogRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<SecurityEventLogDto>> SearchAsync(Guid? tenantId = null, string? searchTerm = null, string? eventTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT s.SecurityEventId, s.TenantId, s.UserId, u.FullName AS UserFullName, u.Email AS UserEmail,
           s.EventTypeCode, s.EventDescription, s.IpAddress, s.UserAgent,
           s.IsSuccess, s.RiskScore, s.SessionId, s.CreatedDateUtc
    FROM Audit.SecurityEventLog s
    LEFT JOIN IAM.[User] u ON u.UserId = s.UserId AND u.IsDeleted = 0
    WHERE s.IsDeleted = 0
      AND (@TenantId IS NULL OR s.TenantId = @TenantId)
      AND (@EventTypeCode IS NULL OR @EventTypeCode = '' OR s.EventTypeCode = @EventTypeCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR s.EventDescription LIKE '%' + @SearchTerm + '%'
           OR s.EventTypeCode    LIKE '%' + @SearchTerm + '%'
           OR s.IpAddress        LIKE '%' + @SearchTerm + '%'
           OR u.FullName         LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT s.SecurityEventId, s.TenantId, s.UserId, u.FullName AS UserFullName, u.Email AS UserEmail,
           s.EventTypeCode, s.EventDescription, s.IpAddress, s.UserAgent,
           s.IsSuccess, s.RiskScore, s.SessionId, s.CreatedDateUtc
    FROM Audit.SecurityEventLog s
    LEFT JOIN IAM.[User] u ON u.UserId = s.UserId AND u.IsDeleted = 0
    WHERE s.IsDeleted = 0
      AND (@TenantId IS NULL OR s.TenantId = @TenantId)
      AND (@EventTypeCode IS NULL OR @EventTypeCode = '' OR s.EventTypeCode = @EventTypeCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR s.EventDescription LIKE '%' + @SearchTerm + '%'
           OR s.EventTypeCode    LIKE '%' + @SearchTerm + '%'
           OR s.IpAddress        LIKE '%' + @SearchTerm + '%'
           OR u.FullName         LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY CreatedDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            EventTypeCode = eventTypeCode,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<SecurityEventLogDto>()).ToList();
        return new PagedResult<SecurityEventLogDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
