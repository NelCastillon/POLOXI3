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
        const string sql = "SELECT UserId, TenantId, BranchId, UserName, Email, FullName, DisplayName, PhoneNumber, UserTypeCode, StatusCode, MfaEnabled, TimeZoneCode, LocaleCode, Department, JobTitle, PasswordChangedDateUtc, IsLockedOut, LockoutEndDateUtc, FailedLoginAttempts, LastLoginDateUtc, CreatedDateUtc, ModifiedDateUtc FROM IAM.[User] WHERE UserId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<UserDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("IAM.[User]", "UserId, TenantId, BranchId, UserName, Email, FullName, DisplayName, PhoneNumber, UserTypeCode, StatusCode, MfaEnabled, TimeZoneCode, LocaleCode, Department, JobTitle, PasswordChangedDateUtc, IsLockedOut, LockoutEndDateUtc, FailedLoginAttempts, LastLoginDateUtc, CreatedDateUtc, ModifiedDateUtc", "FullName LIKE '%' + @SearchTerm + '%' OR Email LIKE '%' + @SearchTerm + '%' OR UserName LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.[User] (UserId, TenantId, BranchId, UserName, Email, FullName, DisplayName, PhoneNumber, UserTypeCode, StatusCode, TimeZoneCode, LocaleCode, Department, JobTitle, MfaEnabled, IsLockedOut, FailedLoginAttempts, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@UserId, @TenantId, @BranchId, @UserName, @Email, @FullName, @DisplayName, @PhoneNumber, @UserTypeCode, 'Active', @TimeZoneCode, @LocaleCode, @Department, @JobTitle, 0, 0, 0, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = id, request.TenantId, request.BranchId, request.UserName, request.Email, request.FullName, request.DisplayName, request.PhoneNumber, request.UserTypeCode, request.TimeZoneCode, request.LocaleCode, request.Department, request.JobTitle, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.[User] SET FullName = @FullName, DisplayName = @DisplayName, PhoneNumber = @PhoneNumber, Department = @Department, JobTitle = @JobTitle, TimeZoneCode = @TimeZoneCode, LocaleCode = @LocaleCode, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserId, request.FullName, request.DisplayName, request.PhoneNumber, request.Department, request.JobTitle, request.TimeZoneCode, request.LocaleCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
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
