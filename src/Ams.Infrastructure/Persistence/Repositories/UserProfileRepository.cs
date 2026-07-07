using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public UserProfileRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<UserProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ProfileHasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;
DECLARE @ProfileHasAvatarColor BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'AvatarColor') IS NULL THEN 0 ELSE 1 END;
DECLARE @ProfileHasCreatedDateUtc BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'CreatedDateUtc') IS NULL THEN 0 ELSE 1 END;
DECLARE @ProfileHasModifiedDateUtc BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'ModifiedDateUtc') IS NULL THEN 0 ELSE 1 END;

DECLARE @Sql NVARCHAR(MAX) = N'
SELECT u.UserId,
       COALESCE(p.PhoneNumber, u.PhoneNumber) AS PhoneNumber,
       p.MobileNumber,
       p.CountryCode,
       p.AddressLine1,
       p.AddressLine2,
       p.City,
       p.StateProvince,
       p.PostalCode,
       p.AvatarUrl,'
       + CASE WHEN @ProfileHasAvatarColor = 1 THEN N'
       p.AvatarColor,' ELSE N'
       CAST(NULL AS NVARCHAR(30)) AS AvatarColor,' END
       + N'
       p.EmergencyContactName,
       p.EmergencyContactPhone,'
       + CASE WHEN @ProfileHasCreatedDateUtc = 1 THEN N'
       p.CreatedDateUtc,' ELSE N'
       CAST(NULL AS DATETIME2) AS CreatedDateUtc,' END
       + CASE WHEN @ProfileHasModifiedDateUtc = 1 THEN N'
       p.ModifiedDateUtc' ELSE N'
       CAST(NULL AS DATETIME2) AS ModifiedDateUtc' END
       + N'
FROM IAM.[User] u
LEFT JOIN IAM.UserProfile p ON p.UserId = u.UserId'
       + CASE WHEN @ProfileHasIsDeleted = 1 THEN N' AND p.IsDeleted = 0' ELSE N'' END
       + N'
WHERE u.UserId = @UserId
  AND u.IsDeleted = 0;';

EXEC sp_executesql @Sql, N'@UserId UNIQUEIDENTIFIER', @UserId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserProfileDto>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TOP 1 TenantId FROM IAM.[User] WHERE UserId = @UserId AND IsDeleted = 0);
DECLARE @HasUserProfileId BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'UserProfileId') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasTenantId BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'TenantId') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasAvatarColor BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'AvatarColor') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasCreatedDateUtc BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'CreatedDateUtc') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasModifiedDateUtc BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'ModifiedDateUtc') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasCreatedByUserId BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'CreatedByUserId') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasModifiedByUserId BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'ModifiedByUserId') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasIsDeleted BIT = CASE WHEN COL_LENGTH('IAM.UserProfile', 'IsDeleted') IS NULL THEN 0 ELSE 1 END;

IF @TenantId IS NULL
    THROW 51000, 'User profile cannot be saved because the user does not exist.', 1;

IF EXISTS (SELECT 1 FROM IAM.UserProfile WHERE UserId = @UserId AND (@HasIsDeleted = 0 OR IsDeleted = 0))
BEGIN
    DECLARE @UpdateSql NVARCHAR(MAX) = N'UPDATE IAM.UserProfile SET
        PhoneNumber = @PhoneNumber,
        MobileNumber = @MobileNumber,
        CountryCode = @CountryCode,
        AddressLine1 = @AddressLine1,
        AddressLine2 = @AddressLine2,
        City = @City,
        StateProvince = @StateProvince,
        PostalCode = @PostalCode,
        AvatarUrl = @AvatarUrl,
        EmergencyContactName = @EmergencyContactName,
        EmergencyContactPhone = @EmergencyContactPhone'
        + CASE WHEN @HasAvatarColor = 1 THEN N', AvatarColor = @AvatarColor' ELSE N'' END
        + CASE WHEN @HasTenantId = 1 THEN N', TenantId = COALESCE(TenantId, @TenantId)' ELSE N'' END
        + CASE WHEN @HasModifiedByUserId = 1 THEN N', ModifiedByUserId = @UserId' ELSE N'' END
        + CASE WHEN @HasModifiedDateUtc = 1 THEN N', ModifiedDateUtc = SYSUTCDATETIME()' ELSE N'' END
        + N' WHERE UserId = @UserId'
        + CASE WHEN @HasIsDeleted = 1 THEN N' AND IsDeleted = 0' ELSE N'' END
        + N';';

    EXEC sp_executesql @UpdateSql,
        N'@UserId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @PhoneNumber NVARCHAR(40), @MobileNumber NVARCHAR(40), @CountryCode NVARCHAR(10), @AddressLine1 NVARCHAR(300), @AddressLine2 NVARCHAR(300), @City NVARCHAR(150), @StateProvince NVARCHAR(150), @PostalCode NVARCHAR(30), @AvatarUrl NVARCHAR(1000), @AvatarColor NVARCHAR(30), @EmergencyContactName NVARCHAR(200), @EmergencyContactPhone NVARCHAR(40)',
        @UserId, @TenantId, @PhoneNumber, @MobileNumber, @CountryCode, @AddressLine1, @AddressLine2, @City, @StateProvince, @PostalCode, @AvatarUrl, @AvatarColor, @EmergencyContactName, @EmergencyContactPhone;
END
ELSE
BEGIN
    DECLARE @InsertColumns NVARCHAR(MAX) = N'UserId, PhoneNumber, MobileNumber, CountryCode, AddressLine1, AddressLine2, City, StateProvince, PostalCode, AvatarUrl, EmergencyContactName, EmergencyContactPhone';
    DECLARE @InsertValues NVARCHAR(MAX) = N'@UserId, @PhoneNumber, @MobileNumber, @CountryCode, @AddressLine1, @AddressLine2, @City, @StateProvince, @PostalCode, @AvatarUrl, @EmergencyContactName, @EmergencyContactPhone';

    IF @HasUserProfileId = 1 BEGIN SET @InsertColumns = N'UserProfileId, ' + @InsertColumns; SET @InsertValues = N'NEWID(), ' + @InsertValues; END;
    IF @HasTenantId = 1 BEGIN SET @InsertColumns = @InsertColumns + N', TenantId'; SET @InsertValues = @InsertValues + N', @TenantId'; END;
    IF @HasAvatarColor = 1 BEGIN SET @InsertColumns = @InsertColumns + N', AvatarColor'; SET @InsertValues = @InsertValues + N', @AvatarColor'; END;
    IF @HasCreatedByUserId = 1 BEGIN SET @InsertColumns = @InsertColumns + N', CreatedByUserId'; SET @InsertValues = @InsertValues + N', @UserId'; END;
    IF @HasCreatedDateUtc = 1 BEGIN SET @InsertColumns = @InsertColumns + N', CreatedDateUtc'; SET @InsertValues = @InsertValues + N', SYSUTCDATETIME()'; END;
    IF @HasIsDeleted = 1 BEGIN SET @InsertColumns = @InsertColumns + N', IsDeleted'; SET @InsertValues = @InsertValues + N', 0'; END;

    DECLARE @InsertSql NVARCHAR(MAX) = N'INSERT INTO IAM.UserProfile (' + @InsertColumns + N') VALUES (' + @InsertValues + N');';
    EXEC sp_executesql @InsertSql,
        N'@UserId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @PhoneNumber NVARCHAR(40), @MobileNumber NVARCHAR(40), @CountryCode NVARCHAR(10), @AddressLine1 NVARCHAR(300), @AddressLine2 NVARCHAR(300), @City NVARCHAR(150), @StateProvince NVARCHAR(150), @PostalCode NVARCHAR(30), @AvatarUrl NVARCHAR(1000), @AvatarColor NVARCHAR(30), @EmergencyContactName NVARCHAR(200), @EmergencyContactPhone NVARCHAR(40)',
        @UserId, @TenantId, @PhoneNumber, @MobileNumber, @CountryCode, @AddressLine1, @AddressLine2, @City, @StateProvince, @PostalCode, @AvatarUrl, @AvatarColor, @EmergencyContactName, @EmergencyContactPhone;
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }
}
