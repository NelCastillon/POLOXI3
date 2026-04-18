using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserRoleRepository : IUserRoleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public UserRoleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<UserRoleDto>> SearchAsync(Guid tenantId, Guid? userId, Guid? roleId, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT ur.UserRoleId, ur.TenantId, ur.UserId, u.FullName AS UserFullName, u.UserName,
           ur.RoleId, r.RoleName, r.RoleCode,
           ab.FullName AS AssignedByFullName, ur.AssignedDateUtc,
           ur.EffectiveStartDateUtc, ur.EffectiveEndDateUtc, ur.IsActive,
           ur.CreatedDateUtc, ur.ModifiedDateUtc
    FROM IAM.UserRole ur
    JOIN IAM.[User] u ON u.UserId = ur.UserId
    JOIN IAM.Role r ON r.RoleId = ur.RoleId
    LEFT JOIN IAM.[User] ab ON ab.UserId = ur.AssignedByUserId
    WHERE ur.TenantId = @TenantId AND ur.IsDeleted = 0
      AND (@UserId IS NULL OR ur.UserId = @UserId)
      AND (@RoleId IS NULL OR ur.RoleId = @RoleId)
      AND (@IsActive IS NULL OR ur.IsActive = @IsActive)
)
SELECT * FROM Cte ORDER BY AssignedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.UserRole ur
WHERE ur.TenantId = @TenantId AND ur.IsDeleted = 0
  AND (@UserId IS NULL OR ur.UserId = @UserId)
  AND (@RoleId IS NULL OR ur.RoleId = @RoleId)
  AND (@IsActive IS NULL OR ur.IsActive = @IsActive);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, RoleId = roleId, IsActive = isActive, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserRoleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserRoleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> AssignAsync(AssignUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.UserRole
    (UserRoleId, TenantId, UserId, RoleId, AssignedByUserId, AssignedDateUtc,
     EffectiveStartDateUtc, EffectiveEndDateUtc, IsActive,
     Source, Reason, ApproverId, ScopeTypeCode, ScopeValue,
     CreatedDateUtc, IsDeleted)
VALUES
    (@UserRoleId, @TenantId, @UserId, @RoleId, @AssignedByUserId, GETUTCDATE(),
     @EffectiveStartDateUtc, @EffectiveEndDateUtc, 1,
     @Source, @Reason, @ApproverId, @ScopeTypeCode, @ScopeValue,
     GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            UserRoleId = id, request.TenantId, request.UserId, request.RoleId,
            request.AssignedByUserId, request.EffectiveStartDateUtc, request.EffectiveEndDateUtc,
            request.Source, request.Reason, request.ApproverId,
            request.ScopeTypeCode, request.ScopeValue
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RevokeAsync(RevokeUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserRole SET IsActive = 0, IsDeleted = 1, Reason = @Reason, ModifiedByUserId = @RevokedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserRoleId = @UserRoleId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserRoleId, request.RevokedByUserId, request.Reason }, cancellationToken: cancellationToken));
    }

    public async Task RemoveAsync(RemoveRoleAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserRole SET IsActive = 0, IsDeleted = 1, Reason = @Reason, ModifiedByUserId = @RemovedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserRoleId = @UserRoleId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserRoleId, request.RemovedByUserId, request.Reason }, cancellationToken: cancellationToken));
    }

    public async Task ApproveAsync(ApproveRoleAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE IAM.UserRole
SET IsActive = 1, ApproverId = @ApprovedByUserId, ApprovedDateUtc = GETUTCDATE(),
    Reason = COALESCE(@Reason, Reason),
    ModifiedByUserId = @ApprovedByUserId, ModifiedDateUtc = GETUTCDATE()
WHERE UserRoleId = @UserRoleId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserRoleId, request.ApprovedByUserId, request.Reason }, cancellationToken: cancellationToken));
    }

    public async Task ExtendAsync(ExtendRoleAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE IAM.UserRole
SET EffectiveEndDateUtc = @NewEndDateUtc, Reason = @Reason,
    ModifiedByUserId = @ExtendedByUserId, ModifiedDateUtc = GETUTCDATE()
WHERE UserRoleId = @UserRoleId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserRoleId, request.NewEndDateUtc, request.Reason, request.ExtendedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<EffectivePermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
-- Role-granted permissions (via active, non-expired user-role assignments)
SELECT @UserId AS UserId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       'Role' AS GrantSource, r.RoleName,
       ur.EffectiveEndDateUtc AS ExpiresDateUtc
FROM IAM.UserRole ur
JOIN IAM.RolePermission rp ON rp.RoleId = ur.RoleId AND rp.IsDeleted = 0
JOIN IAM.Permission p ON p.PermissionId = rp.PermissionId AND p.IsActive = 1
JOIN IAM.Role r ON r.RoleId = ur.RoleId
WHERE ur.UserId = @UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0
  AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > GETUTCDATE())

UNION

-- Direct user grants (IsGranted = 1, not expired)
SELECT @UserId AS UserId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       'Direct' AS GrantSource, NULL AS RoleName,
       up.ExpiresDateUtc
FROM IAM.UserPermission up
JOIN IAM.Permission p ON p.PermissionId = up.PermissionId AND p.IsActive = 1
WHERE up.UserId = @UserId AND up.IsGranted = 1 AND up.IsDeleted = 0
  AND (up.ExpiresDateUtc IS NULL OR up.ExpiresDateUtc > GETUTCDATE())

EXCEPT

-- Explicit user denies
SELECT @UserId AS UserId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       'Deny' AS GrantSource, NULL AS RoleName,
       up.ExpiresDateUtc
FROM IAM.UserPermission up
JOIN IAM.Permission p ON p.PermissionId = up.PermissionId
WHERE up.UserId = @UserId AND up.IsGranted = 0 AND up.IsDeleted = 0
  AND (up.ExpiresDateUtc IS NULL OR up.ExpiresDateUtc > GETUTCDATE());";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<EffectivePermissionDto>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }
}
