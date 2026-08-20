using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Ams.Application.Features.Security;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public UserRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT u.UserId, u.TenantId, u.BranchId, u.UserName, u.Email, u.FullName, u.DisplayName, u.PhoneNumber, u.UserTypeCode, u.StatusCode, u.MfaEnabled, u.TimeZoneCode, u.LocaleCode, u.DepartmentId, COALESCE(d.DepartmentName,u.Department) Department, u.JobTitleId, COALESCE(j.JobTitleName,u.JobTitle) JobTitle, u.PasswordChangedDateUtc, u.IsLockedOut, u.LockoutEndDateUtc, u.FailedLoginAttempts, u.LastLoginDateUtc, u.CreatedDateUtc, u.ModifiedDateUtc,
       (SELECT COUNT(1) FROM IAM.UserRole ur WHERE ur.UserId = u.UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0) AS AssignedRoleCount,
       (SELECT STRING_AGG(r.RoleName, ',') FROM IAM.UserRole ur JOIN IAM.Role r ON r.RoleId = ur.RoleId WHERE ur.UserId = u.UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0) AS AssignedRoleNames,
       (SELECT STRING_AGG(r.RoleCode, ',') FROM IAM.UserRole ur JOIN IAM.Role r ON r.RoleId = ur.RoleId WHERE ur.UserId = u.UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0) AS AssignedRoleCodes
FROM IAM.[User] u
LEFT JOIN Agency.Department d ON d.DepartmentId=u.DepartmentId AND d.TenantId=u.TenantId AND d.IsDeleted=0
LEFT JOIN IAM.JobTitle j ON j.JobTitleId=u.JobTitleId AND j.TenantId=u.TenantId AND j.IsDeleted=0
WHERE u.UserId = @Id AND u.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<UserDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = @"
;WITH Cte AS (
    SELECT u.UserId, u.TenantId, u.BranchId, u.UserName, u.Email, u.FullName, u.DisplayName, u.PhoneNumber, u.UserTypeCode, u.StatusCode, u.MfaEnabled, u.TimeZoneCode, u.LocaleCode, u.DepartmentId, COALESCE(d.DepartmentName,u.Department) Department, u.JobTitleId, COALESCE(j.JobTitleName,u.JobTitle) JobTitle, u.PasswordChangedDateUtc, u.IsLockedOut, u.LockoutEndDateUtc, u.FailedLoginAttempts, u.LastLoginDateUtc, u.CreatedDateUtc, u.ModifiedDateUtc,
           (SELECT COUNT(1) FROM IAM.UserRole ur WHERE ur.UserId = u.UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0) AS AssignedRoleCount,
           (SELECT STRING_AGG(r.RoleName, ',') FROM IAM.UserRole ur JOIN IAM.Role r ON r.RoleId = ur.RoleId WHERE ur.UserId = u.UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0) AS AssignedRoleNames,
           (SELECT STRING_AGG(r.RoleCode, ',') FROM IAM.UserRole ur JOIN IAM.Role r ON r.RoleId = ur.RoleId WHERE ur.UserId = u.UserId AND ur.IsActive = 1 AND ur.IsDeleted = 0) AS AssignedRoleCodes
    FROM IAM.[User] u
    LEFT JOIN Agency.Department d ON d.DepartmentId=u.DepartmentId AND d.TenantId=u.TenantId AND d.IsDeleted=0
    LEFT JOIN IAM.JobTitle j ON j.JobTitleId=u.JobTitleId AND j.TenantId=u.TenantId AND j.IsDeleted=0
    WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%' OR u.Email LIKE '%' + @SearchTerm + '%' OR u.UserName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM IAM.[User] u
WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%' OR u.Email LIKE '%' + @SearchTerm + '%' OR u.UserName LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<JobTitleDto>> GetJobTitlesAsync(Guid tenantId, Guid? departmentId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT title.JobTitleId,title.TenantId,title.JobTitleCode,title.JobTitleName,title.CategoryCode,title.Description,title.IsActive,title.SortOrder
FROM IAM.JobTitle title
WHERE title.TenantId=@TenantId AND title.IsActive=1 AND title.IsDeleted=0
  AND (@DepartmentId IS NULL OR EXISTS
  (
      SELECT 1 FROM Agency.DepartmentJobTitle mapping
      WHERE mapping.TenantId=title.TenantId AND mapping.DepartmentId=@DepartmentId AND mapping.JobTitleId=title.JobTitleId
        AND mapping.IsActive=1 AND mapping.IsDeleted=0
  ))
ORDER BY title.CategoryCode,title.SortOrder,title.JobTitleName;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<JobTitleDto>(new CommandDefinition(sql, new { TenantId = tenantId, DepartmentId = departmentId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"IF @DepartmentId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Agency.Department WHERE DepartmentId=@DepartmentId AND TenantId=@TenantId AND IsActive=1 AND IsDeleted=0)
THROW 51062,N'The selected department is not active for this tenant.',1;
IF @JobTitleId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM IAM.JobTitle WHERE JobTitleId=@JobTitleId AND TenantId=@TenantId AND IsActive=1 AND IsDeleted=0)
THROW 51060,N'The selected job title is not active for this tenant.',1;
IF @JobTitleId IS NOT NULL AND @DepartmentId IS NULL THROW 51063,N'A department is required when a job title is selected.',1;
IF @JobTitleId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Agency.DepartmentJobTitle WHERE TenantId=@TenantId AND DepartmentId=@DepartmentId AND JobTitleId=@JobTitleId AND IsActive=1 AND IsDeleted=0)
THROW 51064,N'The selected job title is not eligible for this department.',1;
DECLARE @DepartmentName NVARCHAR(255)=(SELECT DepartmentName FROM Agency.Department WHERE DepartmentId=@DepartmentId AND TenantId=@TenantId);
DECLARE @JobTitleName NVARCHAR(150)=(SELECT JobTitleName FROM IAM.JobTitle WHERE JobTitleId=@JobTitleId AND TenantId=@TenantId);
INSERT INTO IAM.[User] (UserId,TenantId,BranchId,UserName,Email,FullName,DisplayName,PhoneNumber,UserTypeCode,StatusCode,TimeZoneCode,LocaleCode,DepartmentId,Department,JobTitleId,JobTitle,MfaEnabled,IsLockedOut,FailedLoginAttempts,CreatedByUserId,CreatedDateUtc,IsDeleted)
VALUES (@UserId,@TenantId,@BranchId,@UserName,@Email,@FullName,@DisplayName,@PhoneNumber,@UserTypeCode,'Active',@TimeZoneCode,@LocaleCode,@DepartmentId,@DepartmentName,@JobTitleId,@JobTitleName,0,0,0,@CreatedByUserId,GETUTCDATE(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = id, request.TenantId, request.BranchId, request.UserName, request.Email, request.FullName, request.DisplayName, request.PhoneNumber, request.UserTypeCode, request.TimeZoneCode, request.LocaleCode, request.DepartmentId, request.JobTitleId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @TenantId UNIQUEIDENTIFIER=(SELECT TenantId FROM IAM.[User] WHERE UserId=@UserId AND IsDeleted=0);
IF @TenantId IS NULL THROW 51061,N'User not found.',1;
IF @DepartmentId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Agency.Department WHERE DepartmentId=@DepartmentId AND TenantId=@TenantId AND IsActive=1 AND IsDeleted=0)
    THROW 51062,N'The selected department is not active for this tenant.',1;
IF @JobTitleId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM IAM.JobTitle WHERE JobTitleId=@JobTitleId AND TenantId=@TenantId AND IsActive=1 AND IsDeleted=0)
    THROW 51060,N'The selected job title is not active for this tenant.',1;
IF @JobTitleId IS NOT NULL AND @DepartmentId IS NULL THROW 51063,N'A department is required when a job title is selected.',1;
IF @JobTitleId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Agency.DepartmentJobTitle WHERE TenantId=@TenantId AND DepartmentId=@DepartmentId AND JobTitleId=@JobTitleId AND IsActive=1 AND IsDeleted=0)
    THROW 51064,N'The selected job title is not eligible for this department.',1;
DECLARE @DepartmentName NVARCHAR(255)=(SELECT DepartmentName FROM Agency.Department WHERE DepartmentId=@DepartmentId AND TenantId=@TenantId);
DECLARE @JobTitleName NVARCHAR(150)=(SELECT JobTitleName FROM IAM.JobTitle WHERE JobTitleId=@JobTitleId AND TenantId=@TenantId);
UPDATE IAM.[User] SET FullName=@FullName,DisplayName=@DisplayName,PhoneNumber=@PhoneNumber,DepartmentId=@DepartmentId,Department=@DepartmentName,JobTitleId=@JobTitleId,JobTitle=@JobTitleName,TimeZoneCode=@TimeZoneCode,LocaleCode=@LocaleCode,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=GETUTCDATE() WHERE UserId=@UserId AND IsDeleted=0;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserId, request.FullName, request.DisplayName, request.PhoneNumber, request.DepartmentId, request.JobTitleId, request.TimeZoneCode, request.LocaleCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.[User] SET StatusCode = @StatusCode, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, StatusCode = isActive ? "Active" : "Inactive", ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task LockAsync(Guid userId, DateTime? lockoutEnd, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.[User] SET IsLockedOut = 1, LockoutEndDateUtc = @LockoutEnd, StatusCode = 'Locked', ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, LockoutEnd = lockoutEnd, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task UnlockAsync(Guid userId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.[User] SET IsLockedOut = 0, LockoutEndDateUtc = NULL, FailedLoginAttempts = 0, StatusCode = 'Active', ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task SetMfaAsync(Guid userId, bool enabled, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.[User] SET MfaEnabled = @Enabled, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, Enabled = enabled, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task AssignBranchAsync(Guid userId, Guid? branchId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.[User] SET BranchId = @BranchId, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, BranchId = branchId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task ChangeStatusAsync(ChangeUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var isLocked    = request.NewStatus == "Locked"     ? 1 : 0;
        var clearLock   = request.NewStatus is "Active" or "Suspended" or "Disabled" or "Inactive";
        const string sql = """
            UPDATE IAM.[User]
            SET StatusCode          = @StatusCode,
                IsLockedOut         = CASE WHEN @IsLocked = 1 THEN 1
                                          WHEN @ClearLock = 1 THEN 0
                                          ELSE IsLockedOut END,
                LockoutEndDateUtc   = CASE WHEN @ClearLock = 1 THEN NULL ELSE LockoutEndDateUtc END,
                FailedLoginAttempts = CASE WHEN @ClearLock = 1 THEN 0    ELSE FailedLoginAttempts END,
                ModifiedByUserId    = @ChangedByUserId,
                ModifiedDateUtc     = GETUTCDATE()
            WHERE UserId = @UserId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            UserId         = request.UserId,
            StatusCode     = request.NewStatus,
            IsLocked       = isLocked,
            ClearLock      = clearLock ? 1 : 0,
            ChangedByUserId = request.ChangedByUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<UserPermissionDto>> GetDirectPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT up.UserPermissionId, up.TenantId, up.UserId, u.FullName AS UserFullName,
       up.PermissionId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       up.IsGranted, gb.FullName AS GrantedByFullName, up.GrantedDateUtc, up.ExpiresDateUtc, up.CreatedDateUtc
FROM IAM.UserPermission up
JOIN IAM.[User] u ON u.UserId = up.UserId
JOIN IAM.Permission p ON p.PermissionId = up.PermissionId
LEFT JOIN IAM.[User] gb ON gb.UserId = up.GrantedByUserId
WHERE up.UserId = @UserId AND up.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<UserPermissionDto>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<UserPermissionDto>> GetDirectUsersByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT up.UserPermissionId, up.TenantId, up.UserId, u.FullName AS UserFullName,
       up.PermissionId, p.PermissionCode, p.PermissionName, p.ResourceCode, p.ActionCode,
       up.IsGranted, gb.FullName AS GrantedByFullName, up.GrantedDateUtc, up.ExpiresDateUtc, up.CreatedDateUtc
FROM IAM.UserPermission up
JOIN IAM.[User] u ON u.UserId = up.UserId
JOIN IAM.Permission p ON p.PermissionId = up.PermissionId
LEFT JOIN IAM.[User] gb ON gb.UserId = up.GrantedByUserId
WHERE up.PermissionId = @PermissionId AND up.IsDeleted = 0
ORDER BY u.FullName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<UserPermissionDto>(new CommandDefinition(sql, new { PermissionId = permissionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> GrantPermissionAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.UserPermission
VALUES (@UserPermissionId, @TenantId, @UserId, @PermissionId, @IsGranted, @GrantedByUserId, GETUTCDATE(), @ExpiresDateUtc, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserPermissionId = id, request.TenantId, request.UserId, request.PermissionId, request.IsGranted, request.GrantedByUserId, request.ExpiresDateUtc }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RevokePermissionAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserPermission SET IsDeleted = 1, ModifiedByUserId = @RevokedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserPermissionId = @UserPermissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserPermissionId = userPermissionId, RevokedByUserId = revokedByUserId }, cancellationToken: cancellationToken));
    }
}
