using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PermissionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PermissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.PermissionId, p.TenantId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       p.Description, p.IsBuiltIn, p.IsActive, p.CreatedDateUtc, p.ModifiedDateUtc,
       (SELECT COUNT(1) FROM IAM.RolePermission rp WHERE rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0) AS RoleCount
FROM IAM.Permission p
WHERE p.PermissionId = @Id AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PermissionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PermissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? resourceCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT p.PermissionId, p.TenantId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
           p.Description, p.IsBuiltIn, p.IsActive, p.CreatedDateUtc, p.ModifiedDateUtc,
           (SELECT COUNT(1) FROM IAM.RolePermission rp WHERE rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0) AS RoleCount
    FROM IAM.Permission p
    WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR p.PermissionName LIKE '%' + @SearchTerm + '%' OR p.PermissionCode LIKE '%' + @SearchTerm + '%')
      AND (@ResourceCode IS NULL OR @ResourceCode = '' OR p.ResourceCode = @ResourceCode)
)
SELECT * FROM Cte ORDER BY ResourceCode, ActionCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.Permission p WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR p.PermissionName LIKE '%' + @SearchTerm + '%' OR p.PermissionCode LIKE '%' + @SearchTerm + '%')
  AND (@ResourceCode IS NULL OR @ResourceCode = '' OR p.ResourceCode = @ResourceCode);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, ResourceCode = resourceCode, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PermissionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PermissionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.Permission (PermissionId, TenantId, PermissionCode, PermissionName, ResourceCode, ActionCode, Description, IsBuiltIn, IsActive, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@PermissionId, @TenantId, @PermissionCode, @PermissionName, @ResourceCode, @ActionCode, @Description, @IsBuiltIn, 1, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PermissionId = id, request.TenantId, request.PermissionCode, request.PermissionName, request.ResourceCode, request.ActionCode, request.Description, request.IsBuiltIn, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeactivateAsync(Guid permissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.Permission SET IsActive = 0, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE PermissionId = @PermissionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PermissionId = permissionId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT rp.RolePermissionId, rp.TenantId, rp.RoleId, r.RoleName, rp.PermissionId,
       p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       gb.FullName AS GrantedByFullName, rp.GrantedDateUtc, rp.CreatedDateUtc
FROM IAM.RolePermission rp
JOIN IAM.Role r ON r.RoleId = rp.RoleId
JOIN IAM.Permission p ON p.PermissionId = rp.PermissionId
LEFT JOIN IAM.[User] gb ON gb.UserId = rp.GrantedByUserId
WHERE rp.RoleId = @RoleId AND rp.IsDeleted = 0
ORDER BY p.ResourceCode, p.ActionCode;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<RolePermissionDto>(new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<RolePermissionDto>> GetByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT rp.RolePermissionId, rp.TenantId, rp.RoleId, r.RoleName, rp.PermissionId,
       p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       gb.FullName AS GrantedByFullName, rp.GrantedDateUtc, rp.CreatedDateUtc
FROM IAM.RolePermission rp
JOIN IAM.Role r ON r.RoleId = rp.RoleId
JOIN IAM.Permission p ON p.PermissionId = rp.PermissionId
LEFT JOIN IAM.[User] gb ON gb.UserId = rp.GrantedByUserId
WHERE rp.PermissionId = @PermissionId AND rp.IsDeleted = 0
ORDER BY r.RoleName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<RolePermissionDto>(new CommandDefinition(sql, new { PermissionId = permissionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AssignToRoleAsync(AssignRolePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.RolePermission (RolePermissionId, TenantId, RoleId, PermissionId, GrantedByUserId, GrantedDateUtc, CreatedDateUtc, IsDeleted)
VALUES (@RolePermissionId, @TenantId, @RoleId, @PermissionId, @GrantedByUserId, GETUTCDATE(), GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RolePermissionId = id, request.TenantId, request.RoleId, request.PermissionId, request.GrantedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RevokeFromRoleAsync(RevokeRolePermissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.RolePermission SET IsDeleted = 1, ModifiedByUserId = @RevokedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE RolePermissionId = @RolePermissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.RolePermissionId, request.RevokedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<RolePermissionMatrixDto> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT r.RoleId, r.TenantId, r.RoleCode, r.RoleName, r.RoleTypeCode, r.Description,
       r.IsActive, r.IsBuiltIn, r.IsSystemRole, r.SortOrder, r.CreatedDateUtc, r.ModifiedDateUtc,
       (SELECT COUNT(1) FROM IAM.RolePermission rp2 WHERE rp2.RoleId = r.RoleId AND rp2.IsDeleted = 0) AS PermissionCount,
       (SELECT COUNT(1) FROM IAM.UserRole ur2  WHERE ur2.RoleId  = r.RoleId  AND ur2.IsActive = 1)      AS UserCount
FROM IAM.Role r
WHERE r.TenantId = @TenantId AND r.IsDeleted = 0 AND r.IsActive = 1
ORDER BY r.SortOrder, r.RoleName;

SELECT p.PermissionId, p.TenantId, p.PermissionCode, p.PermissionName,
       p.ResourceCode, p.ActionCode, p.Description, p.IsBuiltIn, p.IsActive,
       p.CreatedDateUtc, p.ModifiedDateUtc,
       (SELECT COUNT(1) FROM IAM.RolePermission rp WHERE rp.PermissionId = p.PermissionId AND rp.IsDeleted = 0) AS RoleCount
FROM IAM.Permission p
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.IsActive = 1
ORDER BY p.ResourceCode, p.ActionCode;

SELECT rp.RolePermissionId, rp.RoleId, rp.PermissionId
FROM IAM.RolePermission rp
JOIN IAM.Role       r ON r.RoleId       = rp.RoleId       AND r.TenantId = @TenantId AND r.IsDeleted = 0
JOIN IAM.Permission p ON p.PermissionId = rp.PermissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0
WHERE rp.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var roles       = (await multi.ReadAsync<RoleDto>()).AsList();
        var permissions = (await multi.ReadAsync<PermissionDto>()).AsList();
        var grants      = (await multi.ReadAsync<MatrixGrantDto>()).AsList();
        return new RolePermissionMatrixDto { Roles = roles, Permissions = permissions, Grants = grants };
    }
}
