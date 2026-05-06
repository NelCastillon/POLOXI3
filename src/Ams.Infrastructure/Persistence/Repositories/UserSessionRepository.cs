using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserSessionRepository : IUserSessionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UserSessionRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<UserSessionDto?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.SessionId, s.TenantId, s.UserId, u.FullName AS UserFullName, u.Email AS UserEmail,
                   s.DeviceType, s.UserAgent, s.IpAddress, s.LoginDateUtc, s.LastActivityDateUtc,
                   s.ExpiresDateUtc, s.IsRevoked, s.RevokedDateUtc, s.RevokedReason, s.CreatedDateUtc
            FROM IAM.UserSession s
            LEFT JOIN IAM.[User] u ON u.UserId = s.UserId
            WHERE s.SessionId = @SessionId AND s.IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserSessionDto>(
            new CommandDefinition(sql, new { SessionId = sessionId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<UserSessionDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT s.SessionId, s.TenantId, s.UserId, u.FullName AS UserFullName, u.Email AS UserEmail,
                       s.DeviceType, s.UserAgent, s.IpAddress, s.LoginDateUtc, s.LastActivityDateUtc,
                       s.ExpiresDateUtc, s.IsRevoked, s.RevokedDateUtc, s.RevokedReason, s.CreatedDateUtc
                FROM IAM.UserSession s
                LEFT JOIN IAM.[User] u ON u.UserId = s.UserId
                WHERE s.TenantId = @TenantId AND s.IsDeleted = 0
                  AND (@UserId IS NULL OR s.UserId = @UserId)
                  AND (@SearchTerm IS NULL OR u.FullName   LIKE '%' + @SearchTerm + '%'
                                          OR u.Email      LIKE '%' + @SearchTerm + '%'
                                          OR s.IpAddress  LIKE '%' + @SearchTerm + '%'
                                          OR s.UserAgent  LIKE '%' + @SearchTerm + '%'
                                          OR s.DeviceType LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY LoginDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1)
            FROM IAM.UserSession s
            LEFT JOIN IAM.[User] u ON u.UserId = s.UserId
            WHERE s.TenantId = @TenantId AND s.IsDeleted = 0
              AND (@UserId IS NULL OR s.UserId = @UserId)
              AND (@SearchTerm IS NULL OR u.FullName   LIKE '%' + @SearchTerm + '%'
                                      OR u.Email      LIKE '%' + @SearchTerm + '%'
                                      OR s.IpAddress  LIKE '%' + @SearchTerm + '%'
                                      OR s.UserAgent  LIKE '%' + @SearchTerm + '%'
                                      OR s.DeviceType LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserSessionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserSessionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task RevokeAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE IAM.UserSession
            SET IsRevoked = 1,
                RevokedDateUtc = COALESCE(RevokedDateUtc, SYSUTCDATETIME()),
                RevokedReason = COALESCE(@Reason, RevokedReason, N'Revoked by Tenant Admin')
            WHERE SessionId = @SessionId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SessionId = sessionId, Reason = reason }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAllAsync(Guid tenantId, Guid? userId = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE IAM.UserSession
            SET IsRevoked = 1,
                RevokedDateUtc = COALESCE(RevokedDateUtc, SYSUTCDATETIME()),
                RevokedReason = COALESCE(@Reason, N'Revoked by Tenant Admin')
            WHERE TenantId = @TenantId
              AND (@UserId IS NULL OR UserId = @UserId)
              AND IsRevoked = 0
              AND ExpiresDateUtc > SYSUTCDATETIME()
              AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, Reason = reason }, cancellationToken: cancellationToken));
    }
}
