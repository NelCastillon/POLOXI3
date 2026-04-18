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
SELECT UserId, PhoneNumber, MobileNumber, CountryCode,
       AddressLine1, AddressLine2, City, StateProvince, PostalCode,
       AvatarUrl, EmergencyContactName, EmergencyContactPhone
FROM IAM.UserProfile
WHERE UserId = @UserId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserProfileDto>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF EXISTS (SELECT 1 FROM IAM.UserProfile WHERE UserId = @UserId)
    UPDATE IAM.UserProfile SET
        PhoneNumber           = @PhoneNumber,
        MobileNumber          = @MobileNumber,
        CountryCode           = @CountryCode,
        AddressLine1          = @AddressLine1,
        AddressLine2          = @AddressLine2,
        City                  = @City,
        StateProvince         = @StateProvince,
        PostalCode            = @PostalCode,
        AvatarUrl             = @AvatarUrl,
        EmergencyContactName  = @EmergencyContactName,
        EmergencyContactPhone = @EmergencyContactPhone
    WHERE UserId = @UserId;
ELSE
    INSERT INTO IAM.UserProfile
        (UserId, PhoneNumber, MobileNumber, CountryCode,
         AddressLine1, AddressLine2, City, StateProvince, PostalCode,
         AvatarUrl, EmergencyContactName, EmergencyContactPhone)
    VALUES
        (@UserId, @PhoneNumber, @MobileNumber, @CountryCode,
         @AddressLine1, @AddressLine2, @City, @StateProvince, @PostalCode,
         @AvatarUrl, @EmergencyContactName, @EmergencyContactPhone);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }
}
