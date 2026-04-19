using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PlatformEventRepository : IPlatformEventRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PlatformEventRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<PlatformEventDto>> SearchAsync(string? searchTerm = null, string? eventTypeCode = null, string? processingStatus = null, string? sourceService = null, Guid? tenantId = null, string? correlationId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT PlatformEventId, EventTypeCode, TenantId, SourceService, TimestampUtc,
           ProcessingStatus, SubscriberCount, CorrelationId, Payload, CreatedDateUtc
    FROM Core.PlatformEvent
    WHERE IsDeleted = 0
      AND (@EventTypeCode    IS NULL OR @EventTypeCode    = '' OR EventTypeCode    = @EventTypeCode)
      AND (@ProcessingStatus IS NULL OR @ProcessingStatus = '' OR ProcessingStatus = @ProcessingStatus)
      AND (@SourceService    IS NULL OR @SourceService    = '' OR SourceService    = @SourceService)
      AND (@TenantId         IS NULL OR TenantId = @TenantId)
      AND (@CorrelationId    IS NULL OR @CorrelationId    = '' OR CorrelationId    = @CorrelationId)
      AND (@SearchTerm       IS NULL OR @SearchTerm       = ''
           OR EventTypeCode  LIKE '%' + @SearchTerm + '%'
           OR SourceService  LIKE '%' + @SearchTerm + '%'
           OR CorrelationId  LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT PlatformEventId, EventTypeCode, TenantId, SourceService, TimestampUtc,
           ProcessingStatus, SubscriberCount, CorrelationId, Payload, CreatedDateUtc
    FROM Core.PlatformEvent
    WHERE IsDeleted = 0
      AND (@EventTypeCode    IS NULL OR @EventTypeCode    = '' OR EventTypeCode    = @EventTypeCode)
      AND (@ProcessingStatus IS NULL OR @ProcessingStatus = '' OR ProcessingStatus = @ProcessingStatus)
      AND (@SourceService    IS NULL OR @SourceService    = '' OR SourceService    = @SourceService)
      AND (@TenantId         IS NULL OR TenantId = @TenantId)
      AND (@CorrelationId    IS NULL OR @CorrelationId    = '' OR CorrelationId    = @CorrelationId)
      AND (@SearchTerm       IS NULL OR @SearchTerm       = ''
           OR EventTypeCode  LIKE '%' + @SearchTerm + '%'
           OR SourceService  LIKE '%' + @SearchTerm + '%'
           OR CorrelationId  LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY TimestampUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm       = searchTerm,
            EventTypeCode    = eventTypeCode,
            ProcessingStatus = processingStatus,
            SourceService    = sourceService,
            TenantId         = tenantId,
            CorrelationId    = correlationId,
            PageNumber       = pageNumber,
            PageSize         = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<PlatformEventDto>()).ToList();
        return new PagedResult<PlatformEventDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<PlatformEventDto?> GetByIdAsync(Guid platformEventId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PlatformEventId, EventTypeCode, TenantId, SourceService, TimestampUtc,
       ProcessingStatus, SubscriberCount, CorrelationId, Payload, CreatedDateUtc
FROM Core.PlatformEvent
WHERE PlatformEventId = @PlatformEventId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PlatformEventDto>(sql, new { PlatformEventId = platformEventId });
    }

    public async Task ReplayAsync(Guid platformEventId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.PlatformEvent
SET ProcessingStatus = 'Pending', ModifiedDateUtc = SYSUTCDATETIME()
WHERE PlatformEventId = @PlatformEventId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { PlatformEventId = platformEventId });
    }
}
