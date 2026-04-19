using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DeploymentStamps;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DeploymentStampRepository : IDeploymentStampRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DeploymentStampRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<DeploymentStampDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT s.StampId, s.StampCode, s.StampName,
           s.RegionId, s.RegionCode, ISNULL(r.RegionName, '') AS RegionName,
           s.EnvironmentCode, s.StatusCode,
           s.TenantCount, s.MaxTenantCapacity, s.LoadPercent, s.ActiveServices,
           s.Notes, s.IsActive, s.CreatedDateUtc, s.ModifiedDateUtc
    FROM Core.DeploymentStamp s
    LEFT JOIN Core.Region r ON r.RegionId = s.RegionId
    WHERE s.IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = '' OR s.StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR s.StampCode  LIKE '%' + @SearchTerm + '%'
           OR s.StampName  LIKE '%' + @SearchTerm + '%'
           OR s.RegionCode LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT s.StampId, s.StampCode, s.StampName,
           s.RegionId, s.RegionCode, ISNULL(r.RegionName, '') AS RegionName,
           s.EnvironmentCode, s.StatusCode,
           s.TenantCount, s.MaxTenantCapacity, s.LoadPercent, s.ActiveServices,
           s.Notes, s.IsActive, s.CreatedDateUtc, s.ModifiedDateUtc
    FROM Core.DeploymentStamp s
    LEFT JOIN Core.Region r ON r.RegionId = s.RegionId
    WHERE s.IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = '' OR s.StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR s.StampCode  LIKE '%' + @SearchTerm + '%'
           OR s.StampName  LIKE '%' + @SearchTerm + '%'
           OR s.RegionCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY StampCode
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
        var items = (await multi.ReadAsync<DeploymentStampDto>()).ToList();
        return new PagedResult<DeploymentStampDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<DeploymentStampDto?> GetByIdAsync(Guid stampId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT s.StampId, s.StampCode, s.StampName,
       s.RegionId, s.RegionCode, ISNULL(r.RegionName, '') AS RegionName,
       s.EnvironmentCode, s.StatusCode,
       s.TenantCount, s.MaxTenantCapacity, s.LoadPercent, s.ActiveServices,
       s.Notes, s.IsActive, s.CreatedDateUtc, s.ModifiedDateUtc
FROM Core.DeploymentStamp s
LEFT JOIN Core.Region r ON r.RegionId = s.RegionId
WHERE s.StampId = @StampId AND s.IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DeploymentStampDto>(sql, new { StampId = stampId });
    }

    public async Task<Guid> CreateAsync(CreateDeploymentStampRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
DECLARE @RegionId UNIQUEIDENTIFIER = (SELECT TOP 1 RegionId FROM Core.Region WHERE RegionCode = @RegionCode AND IsDeleted = 0);
INSERT INTO Core.DeploymentStamp
    (StampId, StampCode, StampName, RegionId, RegionCode, EnvironmentCode, StatusCode,
     MaxTenantCapacity, Notes, IsActive, CreatedDateUtc, CreatedByUserId)
VALUES
    (@NewId, @StampCode, @StampName, @RegionId, @RegionCode, @EnvironmentCode, @StatusCode,
     @MaxTenantCapacity, @Notes, 1, SYSUTCDATETIME(), @CreatedByUserId);
SELECT @NewId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            request.StampCode,
            request.StampName,
            request.RegionCode,
            request.EnvironmentCode,
            request.StatusCode,
            request.MaxTenantCapacity,
            request.Notes,
            request.CreatedByUserId
        });
    }

    public async Task UpdateAsync(Guid stampId, UpdateDeploymentStampRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @RegionId UNIQUEIDENTIFIER = (SELECT TOP 1 RegionId FROM Core.Region WHERE RegionCode = @RegionCode AND IsDeleted = 0);
UPDATE Core.DeploymentStamp SET
    StampCode         = @StampCode,
    StampName         = @StampName,
    RegionId          = @RegionId,
    RegionCode        = @RegionCode,
    EnvironmentCode   = @EnvironmentCode,
    MaxTenantCapacity = @MaxTenantCapacity,
    LoadPercent       = @LoadPercent,
    ActiveServices    = @ActiveServices,
    Notes             = @Notes,
    ModifiedDateUtc   = SYSUTCDATETIME()
WHERE StampId = @StampId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            StampId = stampId,
            request.StampCode,
            request.StampName,
            request.RegionCode,
            request.EnvironmentCode,
            request.MaxTenantCapacity,
            request.LoadPercent,
            request.ActiveServices,
            request.Notes
        });
    }

    public async Task SetStatusAsync(Guid stampId, string statusCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.DeploymentStamp SET
    StatusCode      = @StatusCode,
    IsActive        = CASE WHEN @StatusCode = 'Active' THEN 1 ELSE 0 END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE StampId = @StampId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { StampId = stampId, StatusCode = statusCode });
    }

    public async Task DeleteAsync(Guid stampId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.DeploymentStamp SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHERE StampId = @StampId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { StampId = stampId });
    }
}
