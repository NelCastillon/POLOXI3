using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantDeploymentAssignments;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantDeploymentAssignmentRepository : ITenantDeploymentAssignmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TenantDeploymentAssignmentRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<TenantDeploymentAssignmentDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT a.AssignmentId,
       a.TenantId,
       ISNULL(t.TenantName, '') AS TenantName,
       ISNULL(t.TenantCode, '') AS TenantCode,
       a.EnvironmentCode,
       a.PrimaryRegionCode,
       ISNULL(pr.RegionName, '') AS PrimaryRegionName,
       a.DrRegionCode,
       ISNULL(dr.RegionName, '') AS DrRegionName,
       a.StampCode,
       ISNULL(s.StampName, '') AS StampName,
       a.DatabaseCluster,
       a.StorageBinding,
       a.IsolationMode,
       a.StatusCode,
       a.Notes,
       a.CreatedDateUtc,
       a.ModifiedDateUtc
FROM Core.TenantDeploymentAssignment a
JOIN Core.Tenant t ON t.TenantId = a.TenantId
LEFT JOIN Core.Region pr ON pr.RegionCode = a.PrimaryRegionCode AND pr.IsDeleted = 0
LEFT JOIN Core.Region dr ON dr.RegionCode = a.DrRegionCode AND dr.IsDeleted = 0
LEFT JOIN Core.DeploymentStamp s ON s.StampCode = a.StampCode AND s.IsDeleted = 0
WHERE a.TenantId = @TenantId
  AND a.IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TenantDeploymentAssignmentDto>(sql, new { TenantId = tenantId });
    }

    public async Task<Guid> UpsertAsync(UpsertTenantDeploymentAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER =
    (SELECT AssignmentId FROM Core.TenantDeploymentAssignment WHERE TenantId = @TenantId AND IsDeleted = 0);

IF @ExistingId IS NOT NULL
BEGIN
    UPDATE Core.TenantDeploymentAssignment
    SET EnvironmentCode   = @EnvironmentCode,
        PrimaryRegionCode = @PrimaryRegionCode,
        DrRegionCode      = @DrRegionCode,
        StampCode         = @StampCode,
        DatabaseCluster   = @DatabaseCluster,
        StorageBinding    = @StorageBinding,
        IsolationMode     = @IsolationMode,
        StatusCode        = @StatusCode,
        Notes             = @Notes,
        ModifiedDateUtc   = SYSUTCDATETIME()
    WHERE AssignmentId = @ExistingId;
    SELECT @ExistingId;
END
ELSE
BEGIN
    DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Core.TenantDeploymentAssignment
        (AssignmentId, TenantId, EnvironmentCode, PrimaryRegionCode, DrRegionCode,
         StampCode, DatabaseCluster, StorageBinding, IsolationMode, StatusCode,
         Notes, CreatedDateUtc, CreatedByUserId)
    VALUES
        (@NewId, @TenantId, @EnvironmentCode, @PrimaryRegionCode, @DrRegionCode,
         @StampCode, @DatabaseCluster, @StorageBinding, @IsolationMode, @StatusCode,
         @Notes, SYSUTCDATETIME(), @CreatedByUserId);
    SELECT @NewId;
END;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            request.TenantId,
            request.EnvironmentCode,
            request.PrimaryRegionCode,
            request.DrRegionCode,
            request.StampCode,
            request.DatabaseCluster,
            request.StorageBinding,
            request.IsolationMode,
            request.StatusCode,
            request.Notes,
            request.CreatedByUserId
        });
    }

    public async Task DeleteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.TenantDeploymentAssignment
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { TenantId = tenantId });
    }
}
