using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SystemLogRepository : ISystemLogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SystemLogRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<SystemLogDto>> SearchAsync(string? keyword = null, string? level = null, string? serviceName = null, string? regionCode = null, string? correlationId = null, string? tenantId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT SystemLogId, TenantId, LogLevel, ServiceName, Message, ExceptionText, StackTrace,
           RegionCode, CorrelationId, SourceContext, MachineName, RequestPath, HttpMethod,
           HttpStatusCode, DurationMs, UserId, Properties, CreatedDateUtc
    FROM Audit.SystemLog
    WHERE IsDeleted = 0
      AND (@Level IS NULL OR @Level = '' OR LogLevel = @Level)
      AND (@ServiceName IS NULL OR @ServiceName = '' OR ServiceName = @ServiceName)
      AND (@RegionCode IS NULL OR @RegionCode = '' OR RegionCode = @RegionCode)
      AND (@CorrelationId IS NULL OR @CorrelationId = '' OR CorrelationId = @CorrelationId)
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@Keyword IS NULL OR @Keyword = ''
           OR Message       LIKE '%' + @Keyword + '%'
           OR ServiceName   LIKE '%' + @Keyword + '%'
           OR SourceContext  LIKE '%' + @Keyword + '%'
           OR ExceptionText LIKE '%' + @Keyword + '%'
           OR CorrelationId LIKE '%' + @Keyword + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT SystemLogId, TenantId, LogLevel, ServiceName, Message, ExceptionText, StackTrace,
           RegionCode, CorrelationId, SourceContext, MachineName, RequestPath, HttpMethod,
           HttpStatusCode, DurationMs, UserId, Properties, CreatedDateUtc
    FROM Audit.SystemLog
    WHERE IsDeleted = 0
      AND (@Level IS NULL OR @Level = '' OR LogLevel = @Level)
      AND (@ServiceName IS NULL OR @ServiceName = '' OR ServiceName = @ServiceName)
      AND (@RegionCode IS NULL OR @RegionCode = '' OR RegionCode = @RegionCode)
      AND (@CorrelationId IS NULL OR @CorrelationId = '' OR CorrelationId = @CorrelationId)
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@Keyword IS NULL OR @Keyword = ''
           OR Message       LIKE '%' + @Keyword + '%'
           OR ServiceName   LIKE '%' + @Keyword + '%'
           OR SourceContext  LIKE '%' + @Keyword + '%'
           OR ExceptionText LIKE '%' + @Keyword + '%'
           OR CorrelationId LIKE '%' + @Keyword + '%')
)
SELECT * FROM Cte
ORDER BY CreatedDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        Guid? tenantGuid = Guid.TryParse(tenantId, out var tg) ? tg : null;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            Keyword = keyword,
            Level = level,
            ServiceName = serviceName,
            RegionCode = regionCode,
            CorrelationId = correlationId,
            TenantId = tenantGuid,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<SystemLogDto>()).ToList();
        return new PagedResult<SystemLogDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<SystemLogDto?> GetByIdAsync(Guid systemLogId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT SystemLogId, TenantId, LogLevel, ServiceName, Message, ExceptionText, StackTrace,
       RegionCode, CorrelationId, SourceContext, MachineName, RequestPath, HttpMethod,
       HttpStatusCode, DurationMs, UserId, Properties, CreatedDateUtc
FROM Audit.SystemLog
WHERE SystemLogId = @SystemLogId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SystemLogDto>(sql, new { SystemLogId = systemLogId });
    }
}
