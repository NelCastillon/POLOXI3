using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AuthRepository : IAuthRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AuthRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<LoginCredentialDto?> GetLoginCredentialAsync(Guid tenantId, string userNameOrEmail, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @PermissionHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.Permission', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @PermissionHasIsActive BIT = CASE WHEN COL_LENGTH('IAM.Permission', 'IsActive') IS NULL THEN 0 ELSE 1 END;
DECLARE @RolePermissionHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.RolePermission', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @UserPermissionHasIsGranted BIT = CASE WHEN COL_LENGTH('IAM.UserPermission', 'IsGranted') IS NULL THEN 0 ELSE 1 END;
DECLARE @UserPermissionHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.UserPermission', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @UserPermissionHasExpiresDateUtc BIT = CASE WHEN COL_LENGTH('IAM.UserPermission', 'ExpiresDateUtc') IS NULL THEN 0 ELSE 1 END;

CREATE TABLE #EffectivePermissions
(
    UserId UNIQUEIDENTIFIER NOT NULL,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    PermissionCode NVARCHAR(200) NOT NULL
);

IF OBJECT_ID(N'IAM.RolePermission', N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
    DECLARE @RolePermissionSql NVARCHAR(MAX) = N'
INSERT INTO #EffectivePermissions (UserId, TenantId, PermissionCode)
SELECT DISTINCT ur.UserId, ur.TenantId, p.PermissionCode
FROM IAM.[User] u
INNER JOIN IAM.UserRole ur ON ur.UserId = u.UserId AND ur.TenantId = u.TenantId
INNER JOIN IAM.RolePermission rp ON rp.RoleId = ur.RoleId
INNER JOIN IAM.Permission p ON p.PermissionId = rp.PermissionId
WHERE u.TenantId = @TenantId
  AND u.IsDeleted = 0
  AND (LOWER(u.Email) = LOWER(@UserNameOrEmail) OR LOWER(u.UserName) = LOWER(@UserNameOrEmail))
  AND ur.IsActive = 1
  AND ur.IsDeleted = 0
  AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > SYSUTCDATETIME())'
        + CASE WHEN @RolePermissionHasIsDeleted = 1 THEN N'
  AND rp.IsDeleted = 0' ELSE N'' END
        + CASE WHEN @PermissionHasIsActive = 1 THEN N'
  AND p.IsActive = 1' ELSE N'' END
        + CASE WHEN @PermissionHasIsDeleted = 1 THEN N'
  AND p.IsDeleted = 0' ELSE N'' END;

    EXEC sp_executesql @RolePermissionSql,
        N'@TenantId UNIQUEIDENTIFIER, @UserNameOrEmail NVARCHAR(320)',
        @TenantId, @UserNameOrEmail;
END;

IF OBJECT_ID(N'IAM.UserPermission', N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
    DECLARE @UserPermissionSql NVARCHAR(MAX) = N'
INSERT INTO #EffectivePermissions (UserId, TenantId, PermissionCode)
SELECT DISTINCT up.UserId, up.TenantId, p.PermissionCode
FROM IAM.[User] u
INNER JOIN IAM.UserPermission up ON up.UserId = u.UserId AND up.TenantId = u.TenantId
INNER JOIN IAM.Permission p ON p.PermissionId = up.PermissionId
WHERE u.TenantId = @TenantId
  AND u.IsDeleted = 0
  AND (LOWER(u.Email) = LOWER(@UserNameOrEmail) OR LOWER(u.UserName) = LOWER(@UserNameOrEmail))'
        + CASE WHEN @UserPermissionHasIsGranted = 1 THEN N'
  AND up.IsGranted = 1' ELSE N'' END
        + CASE WHEN @UserPermissionHasIsDeleted = 1 THEN N'
  AND up.IsDeleted = 0' ELSE N'' END
        + CASE WHEN @UserPermissionHasExpiresDateUtc = 1 THEN N'
  AND (up.ExpiresDateUtc IS NULL OR up.ExpiresDateUtc > SYSUTCDATETIME())' ELSE N'' END
        + CASE WHEN @PermissionHasIsActive = 1 THEN N'
  AND p.IsActive = 1' ELSE N'' END
        + CASE WHEN @PermissionHasIsDeleted = 1 THEN N'
  AND p.IsDeleted = 0' ELSE N'' END;

    EXEC sp_executesql @UserPermissionSql,
        N'@TenantId UNIQUEIDENTIFIER, @UserNameOrEmail NVARCHAR(320)',
        @TenantId, @UserNameOrEmail;
END;

SELECT TOP 1
    u.UserId,
    u.TenantId,
    u.UserName,
    u.Email,
    u.FullName,
    u.DisplayName,
    u.StatusCode,
    u.MfaEnabled,
    u.IsLockedOut,
    u.LockoutEndDateUtc,
    u.FailedLoginAttempts,
    u.LastLoginDateUtc,
    u.PasswordHash,
    u.PasswordSalt,
    u.PhoneNumber,
    sms.SmsPhoneNumber,
    (SELECT STRING_AGG(r.RoleCode, ',')
     FROM IAM.UserRole ur
     INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId
     WHERE ur.UserId = u.UserId
       AND ur.TenantId = u.TenantId
       AND ur.IsActive = 1
       AND ur.IsDeleted = 0
       AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > SYSUTCDATETIME())
       AND r.IsActive = 1
       AND r.IsDeleted = 0) AS AssignedRoleCodes,
    (SELECT STRING_AGG(r.RoleName, ',')
     FROM IAM.UserRole ur
     INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId
     WHERE ur.UserId = u.UserId
       AND ur.TenantId = u.TenantId
       AND ur.IsActive = 1
       AND ur.IsDeleted = 0
       AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > SYSUTCDATETIME())
       AND r.IsActive = 1
       AND r.IsDeleted = 0) AS AssignedRoleNames,
     (SELECT STRING_AGG(x.PermissionCode, ',')
      FROM (
         SELECT DISTINCT ep.PermissionCode
         FROM #EffectivePermissions ep
         WHERE ep.UserId = u.UserId
           AND ep.TenantId = u.TenantId
      ) x) AS EffectivePermissionCodes
FROM IAM.[User] u
OUTER APPLY (
    SELECT TOP 1 md.PhoneNumber AS SmsPhoneNumber
    FROM IAM.MfaDevice md
    WHERE md.TenantId = u.TenantId
      AND md.UserId = u.UserId
      AND md.IsActive = 1
      AND md.IsDeleted = 0
      AND md.PhoneNumber IS NOT NULL
      AND LTRIM(RTRIM(md.PhoneNumber)) <> ''
      AND UPPER(md.DeviceTypeCode) = 'SMS'
    ORDER BY md.IsVerified DESC, md.CreatedDateUtc DESC
) sms
WHERE u.TenantId = @TenantId
  AND u.IsDeleted = 0
  AND (LOWER(u.Email) = LOWER(@UserNameOrEmail) OR LOWER(u.UserName) = LOWER(@UserNameOrEmail));";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<LoginCredentialDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserNameOrEmail = userNameOrEmail.Trim() }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> RegisterLoginUserAsync(RegisterLoginUserRequest request, string passwordHash, string passwordSalt, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ExistingUserId UNIQUEIDENTIFIER =
(
    SELECT TOP 1 UserId
    FROM IAM.[User]
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND (LOWER(UserName) = LOWER(@UserName) OR LOWER(Email) = LOWER(@Email))
);

IF @ExistingUserId IS NOT NULL
    THROW 51000, 'A user with this username or email already exists for this tenant.', 1;

INSERT INTO IAM.[User]
(
    UserId, TenantId, BranchId, UserName, Email, FullName, DisplayName, PhoneNumber,
    UserTypeCode, StatusCode, TimeZoneCode, LocaleCode, Department, JobTitle, MfaEnabled,
    PasswordHash, PasswordSalt, PasswordChangedDateUtc, IsLockedOut, FailedLoginAttempts,
    CreatedByUserId, CreatedDateUtc, IsDeleted
)
VALUES
(
    @UserId, @TenantId, @BranchId, @UserName, @Email, @FullName, @DisplayName, @PhoneNumber,
    'Internal', 'Active', NULL, 'en-US', @Department, @JobTitle, @RequireMfa,
    @PasswordHash, @PasswordSalt, SYSUTCDATETIME(), 0, 0,
    @CreatedByUserId, SYSUTCDATETIME(), 0
);

IF NOT EXISTS
(
    SELECT 1
    FROM IAM.UserRole
    WHERE TenantId = @TenantId
      AND UserId = @UserId
      AND RoleId = @RoleId
      AND IsDeleted = 0
)
BEGIN
    INSERT INTO IAM.UserRole
    (
        UserRoleId, TenantId, UserId, RoleId, AssignedByUserId, AssignedDateUtc,
        EffectiveStartDateUtc, IsActive, Source, Reason, ApproverId,
        ScopeTypeCode, ScopeValue, CreatedDateUtc, IsDeleted
    )
    VALUES
    (
        NEWID(), @TenantId, @UserId, @RoleId, @CreatedByUserId, SYSUTCDATETIME(),
        SYSUTCDATETIME(), 1, 'Registration', 'Assigned during enterprise login registration', @CreatedByUserId,
        'Tenant', CONVERT(NVARCHAR(36), @TenantId), SYSUTCDATETIME(), 0
    );
END;";

        var userId = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            UserId = userId,
            request.TenantId,
            request.BranchId,
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim(),
            request.RequireMfa,
            request.RoleId,
            request.CreatedByUserId,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt
        }, cancellationToken: cancellationToken));

        return userId;
    }

    public async Task<TwoFactorChallengeDto> CreateTwoFactorChallengeAsync(Guid tenantId, Guid userId, string phoneNumberMasked, string codeHash, string codeSalt, DateTime expiresDateUtc, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.TwoFactorChallenge
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    FailureReason = 'Superseded'
WHERE TenantId = @TenantId
  AND UserId = @UserId
  AND ConsumedDateUtc IS NULL
  AND ExpiresDateUtc > SYSUTCDATETIME()
  AND IsDeleted = 0;

INSERT INTO IAM.TwoFactorChallenge
(
    TwoFactorChallengeId, TenantId, UserId, DeliveryMethodCode, DestinationMasked,
    CodeHash, CodeSalt, ExpiresDateUtc, MaxAttemptCount, IpAddress, UserAgent,
    CreatedDateUtc, IsDeleted
)
VALUES
(
    @ChallengeId, @TenantId, @UserId, 'SMS', @DestinationMasked,
    @CodeHash, @CodeSalt, @ExpiresDateUtc, 5, @IpAddress, @UserAgent,
    SYSUTCDATETIME(), 0
);

SELECT TwoFactorChallengeId AS ChallengeId,
       TenantId,
       UserId,
       DeliveryMethodCode,
       DestinationMasked,
       ExpiresDateUtc,
       MaxAttemptCount
FROM IAM.TwoFactorChallenge
WHERE TwoFactorChallengeId = @ChallengeId;";

        var challengeId = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<TwoFactorChallengeDto>(new CommandDefinition(sql, new
        {
            ChallengeId = challengeId,
            TenantId = tenantId,
            UserId = userId,
            DestinationMasked = phoneNumberMasked,
            CodeHash = codeHash,
            CodeSalt = codeSalt,
            ExpiresDateUtc = expiresDateUtc,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, cancellationToken: cancellationToken));
    }

    public async Task<TwoFactorChallengeRecordDto?> GetTwoFactorChallengeAsync(Guid tenantId, Guid challengeId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @PermissionHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.Permission', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @PermissionHasIsActive BIT = CASE WHEN COL_LENGTH('IAM.Permission', 'IsActive') IS NULL THEN 0 ELSE 1 END;
DECLARE @RolePermissionHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.RolePermission', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @UserPermissionHasIsGranted BIT = CASE WHEN COL_LENGTH('IAM.UserPermission', 'IsGranted') IS NULL THEN 0 ELSE 1 END;
DECLARE @UserPermissionHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.UserPermission', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @UserPermissionHasExpiresDateUtc BIT = CASE WHEN COL_LENGTH('IAM.UserPermission', 'ExpiresDateUtc') IS NULL THEN 0 ELSE 1 END;

CREATE TABLE #EffectivePermissions
(
    UserId UNIQUEIDENTIFIER NOT NULL,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    PermissionCode NVARCHAR(200) NOT NULL
);

IF OBJECT_ID(N'IAM.RolePermission', N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
    DECLARE @RolePermissionSql NVARCHAR(MAX) = N'
INSERT INTO #EffectivePermissions (UserId, TenantId, PermissionCode)
SELECT DISTINCT ur.UserId, ur.TenantId, p.PermissionCode
FROM IAM.TwoFactorChallenge c
INNER JOIN IAM.UserRole ur ON ur.UserId = c.UserId AND ur.TenantId = c.TenantId
INNER JOIN IAM.RolePermission rp ON rp.RoleId = ur.RoleId
INNER JOIN IAM.Permission p ON p.PermissionId = rp.PermissionId
WHERE c.TenantId = @TenantId
  AND c.TwoFactorChallengeId = @ChallengeId
  AND ur.IsActive = 1
  AND ur.IsDeleted = 0
  AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > SYSUTCDATETIME())'
        + CASE WHEN @RolePermissionHasIsDeleted = 1 THEN N'
  AND rp.IsDeleted = 0' ELSE N'' END
        + CASE WHEN @PermissionHasIsActive = 1 THEN N'
  AND p.IsActive = 1' ELSE N'' END
        + CASE WHEN @PermissionHasIsDeleted = 1 THEN N'
  AND p.IsDeleted = 0' ELSE N'' END;

    EXEC sp_executesql @RolePermissionSql,
        N'@TenantId UNIQUEIDENTIFIER, @ChallengeId UNIQUEIDENTIFIER',
        @TenantId, @ChallengeId;
END;

IF OBJECT_ID(N'IAM.UserPermission', N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
    DECLARE @UserPermissionSql NVARCHAR(MAX) = N'
INSERT INTO #EffectivePermissions (UserId, TenantId, PermissionCode)
SELECT DISTINCT up.UserId, up.TenantId, p.PermissionCode
FROM IAM.TwoFactorChallenge c
INNER JOIN IAM.UserPermission up ON up.UserId = c.UserId AND up.TenantId = c.TenantId
INNER JOIN IAM.Permission p ON p.PermissionId = up.PermissionId
WHERE c.TenantId = @TenantId
  AND c.TwoFactorChallengeId = @ChallengeId'
        + CASE WHEN @UserPermissionHasIsGranted = 1 THEN N'
  AND up.IsGranted = 1' ELSE N'' END
        + CASE WHEN @UserPermissionHasIsDeleted = 1 THEN N'
  AND up.IsDeleted = 0' ELSE N'' END
        + CASE WHEN @UserPermissionHasExpiresDateUtc = 1 THEN N'
  AND (up.ExpiresDateUtc IS NULL OR up.ExpiresDateUtc > SYSUTCDATETIME())' ELSE N'' END
        + CASE WHEN @PermissionHasIsActive = 1 THEN N'
  AND p.IsActive = 1' ELSE N'' END
        + CASE WHEN @PermissionHasIsDeleted = 1 THEN N'
  AND p.IsDeleted = 0' ELSE N'' END;

    EXEC sp_executesql @UserPermissionSql,
        N'@TenantId UNIQUEIDENTIFIER, @ChallengeId UNIQUEIDENTIFIER',
        @TenantId, @ChallengeId;
END;

SELECT TOP 1
    c.TwoFactorChallengeId AS ChallengeId,
    c.TenantId,
    c.UserId,
    c.DeliveryMethodCode,
    c.DestinationMasked,
    c.CodeHash,
    c.CodeSalt,
    c.ExpiresDateUtc,
    c.ConsumedDateUtc,
    c.AttemptCount,
    c.MaxAttemptCount,
    u.UserName,
    u.Email,
    u.FullName,
    u.DisplayName,
    u.MfaEnabled,
    (SELECT STRING_AGG(r.RoleCode, ',')
     FROM IAM.UserRole ur
     INNER JOIN IAM.Role r ON r.RoleId = ur.RoleId
     WHERE ur.UserId = u.UserId
       AND ur.TenantId = u.TenantId
       AND ur.IsActive = 1
       AND ur.IsDeleted = 0
       AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > SYSUTCDATETIME())
       AND r.IsActive = 1
       AND r.IsDeleted = 0) AS AssignedRoleCodes,
    (SELECT STRING_AGG(x.PermissionCode, ',')
     FROM (
        SELECT DISTINCT ep.PermissionCode
        FROM #EffectivePermissions ep
        WHERE ep.UserId = u.UserId
          AND ep.TenantId = u.TenantId
     ) x) AS EffectivePermissionCodes
FROM IAM.TwoFactorChallenge c
INNER JOIN IAM.[User] u ON u.UserId = c.UserId AND u.TenantId = c.TenantId
WHERE c.TenantId = @TenantId
  AND c.TwoFactorChallengeId = @ChallengeId
  AND c.IsDeleted = 0
  AND u.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TwoFactorChallengeRecordDto>(new CommandDefinition(sql, new { TenantId = tenantId, ChallengeId = challengeId }, cancellationToken: cancellationToken));
    }

    public async Task RecordTwoFactorFailureAsync(Guid challengeId, string failureReason, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.TwoFactorChallenge
SET AttemptCount = AttemptCount + 1,
    FailureReason = @FailureReason,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TwoFactorChallengeId = @ChallengeId
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ChallengeId = challengeId, FailureReason = failureReason }, cancellationToken: cancellationToken));
    }

    public async Task ConsumeTwoFactorChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.TwoFactorChallenge
SET ConsumedDateUtc = SYSUTCDATETIME(),
    FailureReason = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TwoFactorChallengeId = @ChallengeId
  AND ConsumedDateUtc IS NULL
  AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ChallengeId = challengeId }, cancellationToken: cancellationToken));
    }

    public async Task RecordLoginAttemptAsync(Guid tenantId, Guid? userId, string userName, string? ipAddress, string? userAgent, bool isSuccessful, string? failureReason, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'IAM.LoginAttempt') IS NOT NULL
BEGIN
    INSERT INTO IAM.LoginAttempt (LoginAttemptId, TenantId, UserId, UserName, IpAddress, UserAgent, IsSuccessful, FailureReason, AttemptDateUtc)
    VALUES (NEWID(), @TenantId, @UserId, @UserName, COALESCE(@IpAddress, 'Unknown'), @UserAgent, @IsSuccessful, @FailureReason, SYSUTCDATETIME());
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, UserName = userName, IpAddress = ipAddress, UserAgent = userAgent, IsSuccessful = isSuccessful, FailureReason = failureReason }, cancellationToken: cancellationToken));
    }

    public async Task RecordLoginSuccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.[User]
SET LastLoginDateUtc = SYSUTCDATETIME(),
    FailedLoginAttempts = 0,
    IsLockedOut = 0,
    LockoutEndDateUtc = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE UserId = @UserId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task RecordLoginFailureAsync(Guid userId, int maxFailedAttempts, TimeSpan lockoutDuration, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.[User]
SET FailedLoginAttempts = FailedLoginAttempts + 1,
    IsLockedOut = CASE WHEN FailedLoginAttempts + 1 >= @MaxFailedAttempts THEN 1 ELSE IsLockedOut END,
    LockoutEndDateUtc = CASE WHEN FailedLoginAttempts + 1 >= @MaxFailedAttempts THEN DATEADD(MINUTE, @LockoutMinutes, SYSUTCDATETIME()) ELSE LockoutEndDateUtc END,
    StatusCode = CASE WHEN FailedLoginAttempts + 1 >= @MaxFailedAttempts THEN 'Locked' ELSE StatusCode END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE UserId = @UserId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, MaxFailedAttempts = maxFailedAttempts, LockoutMinutes = (int)lockoutDuration.TotalMinutes }, cancellationToken: cancellationToken));
    }
}
