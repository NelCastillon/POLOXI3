using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DeploymentBindings;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DeploymentBindingRepository : IDeploymentBindingRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DeploymentBindingRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        db.DeploymentBindingId,
        db.TenantId,
        ISNULL(t.TenantName, '') AS TenantName,
        db.RegionId,
        db.RegionCode,
        ISNULL(r.RegionName, '') AS RegionName,
        db.EnvironmentCode,
        db.StampCode,
        db.IsolationMode,
        db.IsPrimary,
        db.StatusCode,
        db.Notes,
        db.ProvisionedDateUtc,
        db.DecommissionedDateUtc,
        db.CreatedDateUtc,
        db.ModifiedDateUtc";

    public async Task<PagedResult<DeploymentBindingDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT db.DeploymentBindingId, db.TenantId, ISNULL(t.TenantName, '') AS TenantName,
           db.RegionId, db.RegionCode, ISNULL(r.RegionName, '') AS RegionName,
           db.EnvironmentCode, db.StampCode, db.IsolationMode, db.IsPrimary,
           db.StatusCode, db.Notes, db.ProvisionedDateUtc, db.DecommissionedDateUtc,
           db.CreatedDateUtc, db.ModifiedDateUtc
    FROM Core.DeploymentBinding db
    LEFT JOIN Core.Tenant        t ON t.TenantId  = db.TenantId
    LEFT JOIN Core.Region        r ON r.RegionId  = db.RegionId
    WHERE db.IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = '' OR db.StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR db.RegionCode      LIKE '%' + @SearchTerm + '%'
           OR db.StampCode       LIKE '%' + @SearchTerm + '%'
           OR db.EnvironmentCode LIKE '%' + @SearchTerm + '%'
           OR t.TenantName       LIKE '%' + @SearchTerm + '%'
           OR t.TenantCode       LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY TenantName, EnvironmentCode, RegionCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Core.DeploymentBinding db
LEFT JOIN Core.Tenant t ON t.TenantId = db.TenantId
WHERE db.IsDeleted = 0
  AND (@StatusCode IS NULL OR @StatusCode = '' OR db.StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR db.RegionCode      LIKE '%' + @SearchTerm + '%'
       OR db.StampCode       LIKE '%' + @SearchTerm + '%'
       OR db.EnvironmentCode LIKE '%' + @SearchTerm + '%'
       OR t.TenantName       LIKE '%' + @SearchTerm + '%'
       OR t.TenantCode       LIKE '%' + @SearchTerm + '%');";

        var safePage = Math.Max(pageNumber, 1);
        var safeSize = Math.Max(pageSize, 1);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, StatusCode = statusCode, Offset = (safePage - 1) * safeSize, PageSize = safeSize },
            cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<DeploymentBindingDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DeploymentBindingDto> { Items = items, TotalCount = total, PageNumber = safePage, PageSize = safeSize };
    }

    public async Task<DeploymentBindingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT db.DeploymentBindingId, db.TenantId, ISNULL(t.TenantName, '') AS TenantName,
       db.RegionId, db.RegionCode, ISNULL(r.RegionName, '') AS RegionName,
       db.EnvironmentCode, db.StampCode, db.IsolationMode, db.IsPrimary,
       db.StatusCode, db.Notes, db.ProvisionedDateUtc, db.DecommissionedDateUtc,
       db.CreatedDateUtc, db.ModifiedDateUtc
FROM Core.DeploymentBinding db
LEFT JOIN Core.Tenant  t ON t.TenantId = db.TenantId
LEFT JOIN Core.Region  r ON r.RegionId = db.RegionId
WHERE db.DeploymentBindingId = @Id AND db.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DeploymentBindingDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateDeploymentBindingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @RegionId UNIQUEIDENTIFIER = (SELECT TOP 1 RegionId FROM Core.Region WHERE RegionCode = @RegionCode AND IsDeleted = 0);
INSERT INTO Core.DeploymentBinding
    (DeploymentBindingId, TenantId, RegionId, RegionCode, EnvironmentCode, StampCode, IsolationMode, IsPrimary, StatusCode, Notes, CreatedDateUtc)
VALUES
    (@Id, @TenantId, @RegionId, @RegionCode, @EnvironmentCode, @StampCode, @IsolationMode, @IsPrimary, @StatusCode, @Notes, SYSUTCDATETIME());
SELECT @Id;";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            Id              = id,
            TenantId        = request.TenantId,
            RegionCode      = request.RegionCode.Trim().ToUpperInvariant(),
            EnvironmentCode = request.EnvironmentCode,
            StampCode       = request.StampCode,
            IsolationMode   = request.IsolationMode,
            IsPrimary       = request.IsPrimary,
            StatusCode      = request.StatusCode,
            Notes           = request.Notes
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, UpdateDeploymentBindingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @RegionId UNIQUEIDENTIFIER = (SELECT TOP 1 RegionId FROM Core.Region WHERE RegionCode = @RegionCode AND IsDeleted = 0);
UPDATE Core.DeploymentBinding
SET RegionId        = @RegionId,
    RegionCode      = @RegionCode,
    EnvironmentCode = @EnvironmentCode,
    StampCode       = @StampCode,
    IsolationMode   = @IsolationMode,
    IsPrimary       = @IsPrimary,
    StatusCode      = @StatusCode,
    Notes           = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE DeploymentBindingId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id              = id,
            RegionCode      = request.RegionCode.Trim().ToUpperInvariant(),
            EnvironmentCode = request.EnvironmentCode,
            StampCode       = request.StampCode,
            IsolationMode   = request.IsolationMode,
            IsPrimary       = request.IsPrimary,
            StatusCode      = request.StatusCode,
            Notes           = request.Notes
        }, cancellationToken: cancellationToken));
    }

    public async Task SetStatusAsync(Guid id, string statusCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.DeploymentBinding
SET StatusCode      = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE DeploymentBindingId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, StatusCode = statusCode }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.DeploymentBinding SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE DeploymentBindingId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
