using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Security;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TrustedDeviceRepository : ITrustedDeviceRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TrustedDeviceRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<TrustedDeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT td.TrustedDeviceId, td.TenantId, td.UserId,
                   u.FullName AS UserFullName, u.Email,
                   td.DeviceName, td.DeviceFingerprint, td.UserAgent, td.IpAddress,
                   td.DeviceTypeCode, td.BrowserName, td.OperatingSystem,
                   td.TrustedDateUtc, td.ExpiresDateUtc,
                   td.RiskScore, td.RiskFlags, td.RiskNotes,
                   td.IsActive, td.RevokedDateUtc,
                   ru.FullName AS RevokedByUserName, td.RevokedReason,
                   td.LastSeenDateUtc, td.CreatedDateUtc
            FROM IAM.TrustedDevice td
            JOIN  IAM.[User] u  ON u.UserId  = td.UserId
            LEFT JOIN IAM.[User] ru ON ru.UserId = td.RevokedByUserId
            WHERE td.TrustedDeviceId = @Id AND td.IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TrustedDeviceDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TrustedDeviceDto>> SearchAsync(
        Guid tenantId, Guid? userId, string? searchTerm,
        bool? isActive, bool? highRiskOnly,
        int pageNumber = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT td.TrustedDeviceId, td.TenantId, td.UserId,
                       u.FullName AS UserFullName, u.Email,
                       td.DeviceName, td.DeviceFingerprint, td.UserAgent, td.IpAddress,
                       td.DeviceTypeCode, td.BrowserName, td.OperatingSystem,
                       td.TrustedDateUtc, td.ExpiresDateUtc,
                       td.RiskScore, td.RiskFlags, td.RiskNotes,
                       td.IsActive, td.RevokedDateUtc,
                       ru.FullName AS RevokedByUserName, td.RevokedReason,
                       td.LastSeenDateUtc, td.CreatedDateUtc
                FROM IAM.TrustedDevice td
                JOIN  IAM.[User] u  ON u.UserId  = td.UserId
                LEFT JOIN IAM.[User] ru ON ru.UserId = td.RevokedByUserId
                WHERE td.TenantId = @TenantId AND td.IsDeleted = 0
                  AND (@UserId      IS NULL OR td.UserId  = @UserId)
                  AND (@IsActive    IS NULL OR td.IsActive = @IsActive)
                  AND (@HighRiskOnly = 0    OR td.RiskScore >= 70)
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR u.FullName     LIKE '%' + @SearchTerm + '%'
                       OR u.Email        LIKE '%' + @SearchTerm + '%'
                       OR td.DeviceName  LIKE '%' + @SearchTerm + '%'
                       OR td.IpAddress   LIKE '%' + @SearchTerm + '%'
                       OR td.BrowserName LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY TrustedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1)
            FROM IAM.TrustedDevice td
            JOIN  IAM.[User] u  ON u.UserId  = td.UserId
            WHERE td.TenantId = @TenantId AND td.IsDeleted = 0
              AND (@UserId      IS NULL OR td.UserId  = @UserId)
              AND (@IsActive    IS NULL OR td.IsActive = @IsActive)
              AND (@HighRiskOnly = 0    OR td.RiskScore >= 70)
              AND (@SearchTerm IS NULL OR @SearchTerm = ''
                   OR u.FullName     LIKE '%' + @SearchTerm + '%'
                   OR u.Email        LIKE '%' + @SearchTerm + '%'
                   OR td.DeviceName  LIKE '%' + @SearchTerm + '%'
                   OR td.IpAddress   LIKE '%' + @SearchTerm + '%'
                   OR td.BrowserName LIKE '%' + @SearchTerm + '%');
            """;

        var offset        = (Math.Max(pageNumber, 1) - 1) * pageSize;
        var highRiskParam = highRiskOnly == true ? 1 : 0;

        using var cn    = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, UserId = userId, SearchTerm = searchTerm,
                  IsActive = isActive, HighRiskOnly = highRiskParam,
                  Offset = offset, PageSize = pageSize },
            cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<TrustedDeviceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TrustedDeviceDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize,
        };
    }

    public async Task RevokeAsync(RevokeTrustedDeviceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE IAM.TrustedDevice
            SET IsActive         = 0,
                RevokedDateUtc   = GETUTCDATE(),
                RevokedByUserId  = @RevokedByUserId,
                RevokedReason    = @Reason,
                ModifiedDateUtc  = GETUTCDATE(),
                ModifiedByUserId = @RevokedByUserId
            WHERE TrustedDeviceId = @TrustedDeviceId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { request.TrustedDeviceId, request.RevokedByUserId, request.Reason },
            cancellationToken: cancellationToken));
    }

    public async Task SubmitRiskReviewAsync(RiskReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE IAM.TrustedDevice
            SET RiskNotes        = @RiskNotes,
                ModifiedDateUtc  = GETUTCDATE(),
                ModifiedByUserId = @ReviewedByUserId
            WHERE TrustedDeviceId = @TrustedDeviceId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { request.TrustedDeviceId, request.RiskNotes, request.ReviewedByUserId },
            cancellationToken: cancellationToken));
    }
}
