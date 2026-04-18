using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserPermissionRepository : IUserPermissionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public UserPermissionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<UserPermissionDto>> SearchAsync(Guid tenantId, Guid? userId, Guid? permissionId, bool? isGranted, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT up.UserPermissionId, up.TenantId, up.UserId, u.FullName AS UserFullName,
           up.PermissionId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
           up.IsGranted, up.Reason,
           gb.FullName AS GrantedByFullName, up.GrantedDateUtc,
           ab.FullName AS ApprovedByFullName,
           up.EffectiveStartDateUtc, up.ExpiresDateUtc, up.CreatedDateUtc
    FROM IAM.UserPermission up
    JOIN IAM.[User]    u  ON u.UserId       = up.UserId
    JOIN IAM.Permission p  ON p.PermissionId = up.PermissionId
    LEFT JOIN IAM.[User] gb ON gb.UserId    = up.GrantedByUserId
    LEFT JOIN IAM.[User] ab ON ab.UserId    = up.ApprovedByUserId
    WHERE up.TenantId = @TenantId AND up.IsDeleted = 0
      AND (@UserId       IS NULL OR up.UserId       = @UserId)
      AND (@PermissionId IS NULL OR up.PermissionId = @PermissionId)
      AND (@IsGranted    IS NULL OR up.IsGranted    = @IsGranted)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR u.FullName       LIKE '%' + @SearchTerm + '%'
           OR p.PermissionName LIKE '%' + @SearchTerm + '%'
           OR p.PermissionCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY GrantedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM IAM.UserPermission up
JOIN IAM.[User]    u  ON u.UserId       = up.UserId
JOIN IAM.Permission p  ON p.PermissionId = up.PermissionId
WHERE up.TenantId = @TenantId AND up.IsDeleted = 0
  AND (@UserId       IS NULL OR up.UserId       = @UserId)
  AND (@PermissionId IS NULL OR up.PermissionId = @PermissionId)
  AND (@IsGranted    IS NULL OR up.IsGranted    = @IsGranted)
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR u.FullName       LIKE '%' + @SearchTerm + '%'
       OR p.PermissionName LIKE '%' + @SearchTerm + '%'
       OR p.PermissionCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, UserId = userId, PermissionId = permissionId, IsGranted = isGranted,
                  SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserPermissionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserPermissionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> GrantAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.UserPermission (UserPermissionId, TenantId, UserId, PermissionId, IsGranted, GrantedByUserId, GrantedDateUtc, EffectiveStartDateUtc, ExpiresDateUtc, Reason, ApprovedByUserId, CreatedDateUtc, IsDeleted) VALUES (@UserPermissionId, @TenantId, @UserId, @PermissionId, @IsGranted, @GrantedByUserId, GETUTCDATE(), @EffectiveStartDateUtc, @ExpiresDateUtc, @Reason, @ApprovedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserPermissionId = id, request.TenantId, request.UserId, request.PermissionId, request.IsGranted, request.GrantedByUserId, request.EffectiveStartDateUtc, request.ExpiresDateUtc, request.Reason, request.ApprovedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateUserPermissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE IAM.UserPermission SET IsGranted = @IsGranted, EffectiveStartDateUtc = @EffectiveStartDateUtc, ExpiresDateUtc = @EffectiveEndDateUtc, Reason = @Reason, ApprovedByUserId = @ApprovedByUserId, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserPermissionId = @UserPermissionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserPermissionId, request.IsGranted, request.EffectiveStartDateUtc, request.EffectiveEndDateUtc, request.Reason, request.ApprovedByUserId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserPermission SET IsDeleted = 1, ModifiedByUserId = @RevokedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserPermissionId = @UserPermissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserPermissionId = userPermissionId, RevokedByUserId = revokedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UserPermissionScopeDto>> GetScopesAsync(Guid userPermissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT UserPermissionScopeId, UserPermissionId, ScopeTypeCode, ScopeValue, CreatedByUserId, CreatedDateUtc
FROM   IAM.UserPermissionScope
WHERE  UserPermissionId = @UserPermissionId AND IsDeleted = 0
ORDER  BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<UserPermissionScopeDto>(new CommandDefinition(sql, new { UserPermissionId = userPermissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddScopeAsync(AddPermissionScopeRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.UserPermissionScope (UserPermissionScopeId, UserPermissionId, ScopeTypeCode, ScopeValue, CreatedByUserId, CreatedDateUtc, IsDeleted) VALUES (@UserPermissionScopeId, @UserPermissionId, @ScopeTypeCode, @ScopeValue, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserPermissionScopeId = id, request.UserPermissionId, request.ScopeTypeCode, request.ScopeValue, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RemoveScopeAsync(Guid userPermissionScopeId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserPermissionScope SET IsDeleted = 1 WHERE UserPermissionScopeId = @UserPermissionScopeId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserPermissionScopeId = userPermissionScopeId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PermissionConflictDto>> ValidateConflictsAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT a.UserId,
       u.FullName               AS UserFullName,
       a.PermissionId,
       p.PermissionName,
       p.PermissionCode,
       'AllowDenyConflict'      AS ConflictType,
       a.UserPermissionId       AS AllowOverrideId,
       d.UserPermissionId       AS DenyOverrideId
FROM   IAM.UserPermission a
JOIN   IAM.UserPermission d  ON  d.UserId       = a.UserId
                              AND d.PermissionId = a.PermissionId
                              AND d.IsGranted    = 0
                              AND d.IsDeleted    = 0
JOIN   IAM.[User]    u  ON u.UserId       = a.UserId
JOIN   IAM.Permission p  ON p.PermissionId = a.PermissionId
WHERE  a.TenantId  = @TenantId
  AND  a.IsGranted = 1
  AND  a.IsDeleted = 0
  AND  (@UserId IS NULL OR a.UserId = @UserId)
ORDER  BY u.FullName, p.PermissionName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PermissionConflictDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<PermissionScopePreviewDto>> PreviewEffectiveScopeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
-- Role-based permissions not superseded by a direct override
SELECT DISTINCT
    p.PermissionId,
    p.PermissionName,
    p.PermissionCode,
    p.ResourceCode,
    p.ActionCode,
    CAST(1 AS BIT)                  AS IsGranted,
    'Role'                          AS Source,
    r.RoleName,
    CAST(NULL AS UNIQUEIDENTIFIER)  AS OverrideId,
    CAST(NULL AS NVARCHAR(100))     AS ScopeTypeCode,
    CAST(NULL AS NVARCHAR(500))     AS ScopeValue
FROM  IAM.UserRole ur
JOIN  IAM.Role r             ON r.RoleId       = ur.RoleId
JOIN  IAM.RolePermission rp  ON rp.RoleId      = r.RoleId
JOIN  IAM.Permission p       ON p.PermissionId = rp.PermissionId
WHERE ur.TenantId = @TenantId
  AND ur.UserId   = @UserId
  AND ur.IsActive = 1
  AND ur.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM IAM.UserPermission up2
      WHERE up2.TenantId    = @TenantId
        AND up2.UserId      = @UserId
        AND up2.PermissionId = p.PermissionId
        AND up2.IsDeleted   = 0
  )
UNION ALL
-- Direct overrides (each scope row becomes its own preview row)
SELECT
    p.PermissionId,
    p.PermissionName,
    p.PermissionCode,
    p.ResourceCode,
    p.ActionCode,
    up.IsGranted,
    'Override'                      AS Source,
    CAST(NULL AS NVARCHAR(200))     AS RoleName,
    up.UserPermissionId             AS OverrideId,
    ups.ScopeTypeCode,
    ups.ScopeValue
FROM  IAM.UserPermission up
JOIN  IAM.Permission p             ON p.PermissionId      = up.PermissionId
LEFT  JOIN IAM.UserPermissionScope ups ON ups.UserPermissionId = up.UserPermissionId
                                      AND ups.IsDeleted        = 0
WHERE up.TenantId = @TenantId
  AND up.UserId   = @UserId
  AND up.IsDeleted = 0
ORDER BY Source DESC, PermissionName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PermissionScopePreviewDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken))).AsList();
    }
}
