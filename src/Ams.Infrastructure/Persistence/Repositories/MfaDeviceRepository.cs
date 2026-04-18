using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Security;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MfaDeviceRepository : IMfaDeviceRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public MfaDeviceRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<MfaDeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT d.MfaDeviceId, d.TenantId, d.UserId, u.FullName AS UserFullName, d.DeviceTypeCode, d.DeviceName,
       d.PhoneNumber, d.EmailAddress, d.IsVerified, d.IsActive, d.LastUsedDateUtc, d.CreatedDateUtc
FROM IAM.MfaDevice d
JOIN IAM.[User] u ON u.UserId = d.UserId
WHERE d.MfaDeviceId = @Id AND d.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<MfaDeviceDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<MfaDeviceDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT d.MfaDeviceId, d.TenantId, d.UserId, u.FullName AS UserFullName, d.DeviceTypeCode, d.DeviceName,
           d.PhoneNumber, d.EmailAddress, d.IsVerified, d.IsActive, d.LastUsedDateUtc, d.CreatedDateUtc
    FROM IAM.MfaDevice d
    JOIN IAM.[User] u ON u.UserId = d.UserId
    WHERE d.TenantId = @TenantId AND d.IsDeleted = 0
      AND (@UserId IS NULL OR d.UserId = @UserId)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR d.DeviceName LIKE '%' + @SearchTerm + '%' OR u.FullName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.MfaDevice d JOIN IAM.[User] u ON u.UserId = d.UserId
WHERE d.TenantId = @TenantId AND d.IsDeleted = 0
  AND (@UserId IS NULL OR d.UserId = @UserId)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR d.DeviceName LIKE '%' + @SearchTerm + '%' OR u.FullName LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<MfaDeviceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<MfaDeviceDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<UserMfaStatusDto>> SearchUsersWithMfaAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT u.UserId, u.FullName AS UserFullName, u.Email,
           COUNT(d.MfaDeviceId) AS DeviceCount,
           SUM(CASE WHEN d.IsVerified = 1 THEN 1 ELSE 0 END) AS VerifiedDeviceCount,
           CAST(1 AS BIT) AS HasActiveMfa,
           CAST(u.MfaEnabled AS BIT) AS MfaRequired,
           MAX(d.LastUsedDateUtc) AS LastMfaUsedDateUtc
    FROM IAM.[User] u
    INNER JOIN IAM.MfaDevice d ON d.UserId = u.UserId AND d.IsDeleted = 0 AND d.IsActive = 1
    WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR u.FullName LIKE '%' + @SearchTerm + '%'
           OR u.Email   LIKE '%' + @SearchTerm + '%')
    GROUP BY u.UserId, u.FullName, u.Email, u.MfaEnabled
)
SELECT * FROM Cte ORDER BY UserFullName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(DISTINCT u.UserId)
FROM IAM.[User] u
INNER JOIN IAM.MfaDevice d ON d.UserId = u.UserId AND d.IsDeleted = 0 AND d.IsActive = 1
WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR u.FullName LIKE '%' + @SearchTerm + '%'
       OR u.Email   LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserMfaStatusDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserMfaStatusDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<UserMfaStatusDto>> SearchUsersWithoutMfaAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT u.UserId, u.FullName AS UserFullName, u.Email,
           0 AS DeviceCount,
           0 AS VerifiedDeviceCount,
           CAST(0 AS BIT) AS HasActiveMfa,
           CAST(u.MfaEnabled AS BIT) AS MfaRequired,
           NULL AS LastMfaUsedDateUtc
    FROM IAM.[User] u
    WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
      AND NOT EXISTS (
          SELECT 1 FROM IAM.MfaDevice d
          WHERE d.UserId = u.UserId AND d.IsDeleted = 0 AND d.IsActive = 1
      )
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR u.FullName LIKE '%' + @SearchTerm + '%'
           OR u.Email   LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY UserFullName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM IAM.[User] u
WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM IAM.MfaDevice d
      WHERE d.UserId = u.UserId AND d.IsDeleted = 0 AND d.IsActive = 1
  )
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR u.FullName LIKE '%' + @SearchTerm + '%'
       OR u.Email   LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserMfaStatusDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserMfaStatusDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<MfaDeviceDto>> GetUserDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT d.MfaDeviceId, d.TenantId, d.UserId, u.FullName AS UserFullName, d.DeviceTypeCode, d.DeviceName,
       d.PhoneNumber, d.EmailAddress, d.IsVerified, d.IsActive, d.LastUsedDateUtc, d.CreatedDateUtc
FROM IAM.MfaDevice d
JOIN IAM.[User] u ON u.UserId = d.UserId
WHERE d.UserId = @UserId AND d.IsDeleted = 0
ORDER BY d.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<MfaDeviceDto>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddMethodAsync(AddMfaMethodRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO IAM.MfaDevice
    (MfaDeviceId, TenantId, UserId, DeviceTypeCode, DeviceName, PhoneNumber, EmailAddress,
     IsVerified, IsActive, IsDeleted, CreatedByUserId, CreatedDateUtc)
VALUES
    (@MfaDeviceId, @TenantId, @UserId, @DeviceTypeCode, @DeviceName, @PhoneNumber, @EmailAddress,
     0, 1, 0, @CreatedByUserId, GETUTCDATE());";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            MfaDeviceId    = id,
            request.TenantId,
            request.UserId,
            request.DeviceTypeCode,
            request.DeviceName,
            request.PhoneNumber,
            request.EmailAddress,
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task VerifyMethodAsync(VerifyMfaMethodRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.MfaDevice
SET IsVerified = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @VerifiedByUserId
WHERE MfaDeviceId = @MfaDeviceId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.MfaDeviceId, request.VerifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DisableMethodAsync(DisableMfaMethodRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.MfaDevice
SET IsActive = 0, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @DisabledByUserId
WHERE MfaDeviceId = @MfaDeviceId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.MfaDeviceId, request.DisabledByUserId }, cancellationToken: cancellationToken));
    }

    public async Task ResetMfaAsync(ResetMfaRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.MfaDevice
SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @ResetByUserId
WHERE UserId = @UserId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserId, request.ResetByUserId }, cancellationToken: cancellationToken));
    }

    public async Task RequireMfaAsync(RequireMfaRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.[User]
SET MfaEnabled = @IsRequired, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId = @SetByUserId
WHERE UserId = @UserId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.UserId, request.IsRequired, request.SetByUserId }, cancellationToken: cancellationToken));
    }
}
