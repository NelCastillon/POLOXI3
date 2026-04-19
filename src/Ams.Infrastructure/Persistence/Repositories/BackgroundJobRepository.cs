using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BackgroundJobRepository : IBackgroundJobRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BackgroundJobRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<BackgroundJobDto>> SearchAsync(string? searchTerm = null, string? jobTypeCode = null, string? statusCode = null, Guid? tenantId = null, bool? failedOnly = null, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT BackgroundJobId, JobTypeCode, TenantId, StatusCode, CreatedDateUtc,
           StartedDateUtc, CompletedDateUtc, DurationMs, RetryCount, CorrelationId,
           ErrorMessage, Payload, ResultSummary
    FROM Core.BackgroundJob
    WHERE IsDeleted = 0
      AND (@JobTypeCode IS NULL OR @JobTypeCode = '' OR JobTypeCode = @JobTypeCode)
      AND (@StatusCode  IS NULL OR @StatusCode  = '' OR StatusCode  = @StatusCode)
      AND (@TenantId    IS NULL OR TenantId = @TenantId)
      AND (@FailedOnly  IS NULL OR @FailedOnly = 0 OR StatusCode = 'Failed')
      AND (@FromDateUtc IS NULL OR CreatedDateUtc >= @FromDateUtc)
      AND (@ToDateUtc   IS NULL OR CreatedDateUtc <= @ToDateUtc)
      AND (@SearchTerm  IS NULL OR @SearchTerm = ''
           OR JobTypeCode   LIKE '%' + @SearchTerm + '%'
           OR CorrelationId LIKE '%' + @SearchTerm + '%'
           OR ErrorMessage  LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT BackgroundJobId, JobTypeCode, TenantId, StatusCode, CreatedDateUtc,
           StartedDateUtc, CompletedDateUtc, DurationMs, RetryCount, CorrelationId,
           ErrorMessage, Payload, ResultSummary
    FROM Core.BackgroundJob
    WHERE IsDeleted = 0
      AND (@JobTypeCode IS NULL OR @JobTypeCode = '' OR JobTypeCode = @JobTypeCode)
      AND (@StatusCode  IS NULL OR @StatusCode  = '' OR StatusCode  = @StatusCode)
      AND (@TenantId    IS NULL OR TenantId = @TenantId)
      AND (@FailedOnly  IS NULL OR @FailedOnly = 0 OR StatusCode = 'Failed')
      AND (@FromDateUtc IS NULL OR CreatedDateUtc >= @FromDateUtc)
      AND (@ToDateUtc   IS NULL OR CreatedDateUtc <= @ToDateUtc)
      AND (@SearchTerm  IS NULL OR @SearchTerm = ''
           OR JobTypeCode   LIKE '%' + @SearchTerm + '%'
           OR CorrelationId LIKE '%' + @SearchTerm + '%'
           OR ErrorMessage  LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY CreatedDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm  = searchTerm,
            JobTypeCode = jobTypeCode,
            StatusCode  = statusCode,
            TenantId    = tenantId,
            FailedOnly  = failedOnly,
            FromDateUtc = fromDateUtc,
            ToDateUtc   = toDateUtc,
            PageNumber  = pageNumber,
            PageSize    = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<BackgroundJobDto>()).ToList();
        return new PagedResult<BackgroundJobDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<BackgroundJobDto?> GetByIdAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT BackgroundJobId, JobTypeCode, TenantId, StatusCode, CreatedDateUtc,
       StartedDateUtc, CompletedDateUtc, DurationMs, RetryCount, CorrelationId,
       ErrorMessage, Payload, ResultSummary
FROM Core.BackgroundJob
WHERE BackgroundJobId = @BackgroundJobId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<BackgroundJobDto>(sql, new { BackgroundJobId = backgroundJobId });
    }

    public async Task RetryAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.BackgroundJob
SET StatusCode = 'Queued', RetryCount = RetryCount + 1, ErrorMessage = NULL, ModifiedDateUtc = SYSUTCDATETIME()
WHERE BackgroundJobId = @BackgroundJobId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { BackgroundJobId = backgroundJobId });
    }

    public async Task CancelAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.BackgroundJob
SET StatusCode = 'Cancelled', ModifiedDateUtc = SYSUTCDATETIME()
WHERE BackgroundJobId = @BackgroundJobId AND IsDeleted = 0 AND StatusCode IN ('Queued', 'Running');";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { BackgroundJobId = backgroundJobId });
    }

    public async Task RequeueAsync(Guid backgroundJobId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.BackgroundJob
SET StatusCode = 'Queued', StartedDateUtc = NULL, CompletedDateUtc = NULL, DurationMs = 0, ErrorMessage = NULL, ModifiedDateUtc = SYSUTCDATETIME()
WHERE BackgroundJobId = @BackgroundJobId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { BackgroundJobId = backgroundJobId });
    }
}
