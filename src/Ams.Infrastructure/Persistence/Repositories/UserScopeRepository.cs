using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserScopeRepository : IUserScopeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public UserScopeRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<UserScopeDto>> SearchAsync(Guid tenantId, Guid? userId, string? scopeTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT us.UserScopeId, us.TenantId, us.UserId, u.FullName AS UserFullName,
           us.ScopeTypeCode, us.ScopeValue, us.IsActive,
           gb.FullName AS GrantedByFullName, us.GrantedDateUtc, us.ExpiresDateUtc, us.CreatedDateUtc
    FROM IAM.UserScope us
    JOIN IAM.[User] u ON u.UserId = us.UserId
    LEFT JOIN IAM.[User] gb ON gb.UserId = us.GrantedByUserId
    WHERE us.TenantId = @TenantId AND us.IsDeleted = 0
      AND (@UserId IS NULL OR us.UserId = @UserId)
      AND (@ScopeTypeCode IS NULL OR @ScopeTypeCode = '' OR us.ScopeTypeCode = @ScopeTypeCode)
)
SELECT * FROM Cte ORDER BY GrantedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.UserScope us
WHERE us.TenantId = @TenantId AND us.IsDeleted = 0
  AND (@UserId IS NULL OR us.UserId = @UserId)
  AND (@ScopeTypeCode IS NULL OR @ScopeTypeCode = '' OR us.ScopeTypeCode = @ScopeTypeCode);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ScopeTypeCode = scopeTypeCode, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserScopeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserScopeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IEnumerable<UserScopeDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT us.UserScopeId, us.TenantId, us.UserId, u.FullName AS UserFullName,
       us.ScopeTypeCode, us.ScopeValue, us.IsActive,
       gb.FullName AS GrantedByFullName, us.GrantedDateUtc, us.ExpiresDateUtc, us.CreatedDateUtc
FROM IAM.UserScope us
JOIN IAM.[User] u ON u.UserId = us.UserId
LEFT JOIN IAM.[User] gb ON gb.UserId = us.GrantedByUserId
WHERE us.UserId = @UserId AND us.IsActive = 1 AND us.IsDeleted = 0
  AND (us.ExpiresDateUtc IS NULL OR us.ExpiresDateUtc > GETUTCDATE())
ORDER BY us.ScopeTypeCode;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<UserScopeDto>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AssignAsync(AssignUserScopeRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.UserScope (UserScopeId, TenantId, UserId, ScopeTypeCode, ScopeValue, IsActive, GrantedByUserId, GrantedDateUtc, ExpiresDateUtc, CreatedDateUtc, IsDeleted)
VALUES (@UserScopeId, @TenantId, @UserId, @ScopeTypeCode, @ScopeValue, 1, @GrantedByUserId, GETUTCDATE(), @ExpiresDateUtc, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserScopeId = id, request.TenantId, request.UserId, request.ScopeTypeCode, request.ScopeValue, request.GrantedByUserId, request.ExpiresDateUtc }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RevokeAsync(Guid userScopeId, Guid? revokedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserScope SET IsActive = 0, IsDeleted = 1, ModifiedByUserId = @RevokedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE UserScopeId = @UserScopeId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { UserScopeId = userScopeId, RevokedByUserId = revokedByUserId }, cancellationToken: cancellationToken));
    }
}
