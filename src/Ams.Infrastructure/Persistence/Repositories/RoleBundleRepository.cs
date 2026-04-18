using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RoleBundleRepository : IRoleBundleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RoleBundleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RoleBundleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT b.BundleId, b.TenantId, b.BundleCode, b.BundleName, b.Description,
       b.IsSystemBundle, b.IsActive, b.SortOrder, b.CreatedDateUtc, b.ModifiedDateUtc,
       (SELECT COUNT(1) FROM IAM.BundleRole br  WHERE br.BundleId = b.BundleId AND br.IsDeleted = 0) AS RoleCount,
       (SELECT COUNT(1) FROM IAM.BundleUser bu  WHERE bu.BundleId = b.BundleId AND bu.IsDeleted = 0) AS UserCount
FROM IAM.RoleBundle b
WHERE b.BundleId = @Id AND b.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RoleBundleDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RoleBundleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT b.BundleId, b.TenantId, b.BundleCode, b.BundleName, b.Description,
           b.IsSystemBundle, b.IsActive, b.SortOrder, b.CreatedDateUtc, b.ModifiedDateUtc,
           (SELECT COUNT(1) FROM IAM.BundleRole br  WHERE br.BundleId = b.BundleId AND br.IsDeleted = 0) AS RoleCount,
           (SELECT COUNT(1) FROM IAM.BundleUser bu  WHERE bu.BundleId = b.BundleId AND bu.IsDeleted = 0) AS UserCount
    FROM IAM.RoleBundle b
    WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR b.BundleName LIKE '%' + @SearchTerm + '%'
           OR b.BundleCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY SortOrder, BundleCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1) FROM IAM.RoleBundle b
WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR b.BundleName LIKE '%' + @SearchTerm + '%'
       OR b.BundleCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, SearchTerm = searchTerm,
                  Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RoleBundleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RoleBundleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateRoleBundleRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO IAM.RoleBundle
    (BundleId, TenantId, BundleCode, BundleName, Description,
     IsSystemBundle, IsActive, SortOrder, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
    (@BundleId, @TenantId, @BundleCode, @BundleName, @Description,
     @IsSystemBundle, 1, @SortOrder, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { BundleId = id, request.TenantId, request.BundleCode, request.BundleName,
                  request.Description, request.IsSystemBundle, request.SortOrder, request.CreatedByUserId },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateRoleBundleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.RoleBundle
SET BundleName = @BundleName, Description = @Description, SortOrder = @SortOrder,
    ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE()
WHERE BundleId = @BundleId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { request.BundleId, request.BundleName, request.Description,
                  request.SortOrder, request.ModifiedByUserId },
            cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(Guid bundleId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.RoleBundle
SET IsActive = @IsActive, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE()
WHERE BundleId = @BundleId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { BundleId = bundleId, IsActive = isActive, ModifiedByUserId = modifiedByUserId },
            cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<BundleRoleDto>> GetRolesAsync(Guid bundleId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT br.BundleRoleId, br.BundleId, br.RoleId,
       r.RoleCode, r.RoleName, r.RoleTypeCode, r.IsActive, br.AssignedDateUtc
FROM IAM.BundleRole br
INNER JOIN IAM.Role r ON r.RoleId = br.RoleId AND r.IsDeleted = 0
WHERE br.BundleId = @BundleId AND br.IsDeleted = 0
ORDER BY r.RoleName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<BundleRoleDto>(
            new CommandDefinition(sql, new { BundleId = bundleId }, cancellationToken: cancellationToken));
    }

    public async Task SetRolesAsync(SetBundleRolesRequest request, CancellationToken cancellationToken = default)
    {
        const string deleteSql  = "UPDATE IAM.BundleRole SET IsDeleted = 1 WHERE BundleId = @BundleId;";
        const string upsertSql  = @"
IF EXISTS (SELECT 1 FROM IAM.BundleRole WHERE BundleId = @BundleId AND RoleId = @RoleId)
    UPDATE IAM.BundleRole SET IsDeleted = 0 WHERE BundleId = @BundleId AND RoleId = @RoleId;
ELSE
    INSERT INTO IAM.BundleRole (BundleRoleId, BundleId, RoleId, AssignedDateUtc, IsDeleted)
    VALUES (NEWID(), @BundleId, @RoleId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(deleteSql, new { request.BundleId }, cancellationToken: cancellationToken));
        foreach (var roleId in request.RoleIds)
            await cn.ExecuteAsync(new CommandDefinition(upsertSql,
                new { request.BundleId, RoleId = roleId }, cancellationToken: cancellationToken));
    }

    public async Task AssignToUsersAsync(AssignBundleToUsersRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM IAM.BundleUser WHERE BundleId = @BundleId AND UserId = @UserId AND IsDeleted = 0)
    INSERT INTO IAM.BundleUser (BundleUserId, BundleId, UserId, AssignedByUserId, AssignedDateUtc, IsDeleted)
    VALUES (NEWID(), @BundleId, @UserId, @AssignedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        foreach (var userId in request.UserIds)
            await cn.ExecuteAsync(new CommandDefinition(sql,
                new { request.BundleId, UserId = userId, request.AssignedByUserId },
                cancellationToken: cancellationToken));
    }
}
