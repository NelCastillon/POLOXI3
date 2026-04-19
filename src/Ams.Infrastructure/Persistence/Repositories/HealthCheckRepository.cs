using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.HealthChecks;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class HealthCheckRepository : IHealthCheckRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public HealthCheckRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<HealthCheckDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT HealthCheckId, ServiceName, EndpointUrl, StatusCode, LatencyMs, UptimePercent,
           LastCheckDateUtc, Notes, RegionCode, EnvironmentCode, IsActive, CreatedDateUtc, ModifiedDateUtc
    FROM Core.HealthCheck
    WHERE IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR ServiceName  LIKE '%' + @SearchTerm + '%'
           OR RegionCode   LIKE '%' + @SearchTerm + '%'
           OR EndpointUrl  LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT HealthCheckId, ServiceName, EndpointUrl, StatusCode, LatencyMs, UptimePercent,
           LastCheckDateUtc, Notes, RegionCode, EnvironmentCode, IsActive, CreatedDateUtc, ModifiedDateUtc
    FROM Core.HealthCheck
    WHERE IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR ServiceName  LIKE '%' + @SearchTerm + '%'
           OR RegionCode   LIKE '%' + @SearchTerm + '%'
           OR EndpointUrl  LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY ServiceName
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
        var items = (await multi.ReadAsync<HealthCheckDto>()).ToList();
        return new PagedResult<HealthCheckDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<HealthCheckDto?> GetByIdAsync(Guid healthCheckId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT HealthCheckId, ServiceName, EndpointUrl, StatusCode, LatencyMs, UptimePercent,
       LastCheckDateUtc, Notes, RegionCode, EnvironmentCode, IsActive, CreatedDateUtc, ModifiedDateUtc
FROM Core.HealthCheck
WHERE HealthCheckId = @HealthCheckId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<HealthCheckDto>(sql, new { HealthCheckId = healthCheckId });
    }

    public async Task<Guid> CreateAsync(CreateHealthCheckRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Core.HealthCheck
    (HealthCheckId, ServiceName, EndpointUrl, StatusCode, LatencyMs, UptimePercent,
     RegionCode, EnvironmentCode, Notes, CreatedDateUtc, CreatedByUserId)
VALUES
    (@NewId, @ServiceName, @EndpointUrl, @StatusCode, @LatencyMs, @UptimePercent,
     @RegionCode, @EnvironmentCode, @Notes, SYSUTCDATETIME(), @CreatedByUserId);
SELECT @NewId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            request.ServiceName, request.EndpointUrl, request.StatusCode,
            request.LatencyMs, request.UptimePercent, request.RegionCode,
            request.EnvironmentCode, request.Notes, request.CreatedByUserId
        });
    }

    public async Task UpdateAsync(Guid healthCheckId, UpdateHealthCheckRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.HealthCheck
SET ServiceName     = @ServiceName,
    EndpointUrl     = @EndpointUrl,
    StatusCode      = @StatusCode,
    LatencyMs       = @LatencyMs,
    UptimePercent   = @UptimePercent,
    RegionCode      = @RegionCode,
    EnvironmentCode = @EnvironmentCode,
    IsActive        = @IsActive,
    Notes           = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE HealthCheckId = @HealthCheckId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            HealthCheckId = healthCheckId,
            request.ServiceName, request.EndpointUrl, request.StatusCode,
            request.LatencyMs, request.UptimePercent, request.RegionCode,
            request.EnvironmentCode, request.IsActive, request.Notes
        });
    }

    public async Task DeleteAsync(Guid healthCheckId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.HealthCheck SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE HealthCheckId = @HealthCheckId;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { HealthCheckId = healthCheckId });
    }
}
