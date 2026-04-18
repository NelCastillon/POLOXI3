using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PrivilegedAccessRepository : IPrivilegedAccessRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PrivilegedAccessRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PrivilegedAccessRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT r.RequestId, r.TenantId, r.RequestedByUserId, u.FullName AS RequestedByFullName,
       r.TargetRoleId, ro.RoleName AS TargetRoleName, r.JustificationText,
       r.RequestedStartDateUtc, r.RequestedEndDateUtc, ua.FullName AS ApprovedByFullName,
       r.ApprovalDateUtc, r.GrantedStartDateUtc, r.GrantedEndDateUtc, r.StatusCode,
       r.RevokedReason, r.RevokedDateUtc, r.CreatedDateUtc
FROM IAM.PrivilegedAccessRequest r
JOIN IAM.[User] u ON u.UserId = r.RequestedByUserId
JOIN IAM.Role ro ON ro.RoleId = r.TargetRoleId
LEFT JOIN IAM.[User] ua ON ua.UserId = r.ApprovedByUserId
WHERE r.RequestId = @Id AND r.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PrivilegedAccessRequestDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PrivilegedAccessRequestDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT r.RequestId, r.TenantId, r.RequestedByUserId, u.FullName AS RequestedByFullName,
           r.TargetRoleId, ro.RoleName AS TargetRoleName, r.JustificationText,
           r.RequestedStartDateUtc, r.RequestedEndDateUtc, ua.FullName AS ApprovedByFullName,
           r.ApprovalDateUtc, r.GrantedStartDateUtc, r.GrantedEndDateUtc, r.StatusCode,
           r.RevokedReason, r.RevokedDateUtc, r.CreatedDateUtc
    FROM IAM.PrivilegedAccessRequest r
    JOIN IAM.[User] u ON u.UserId = r.RequestedByUserId
    JOIN IAM.Role ro ON ro.RoleId = r.TargetRoleId
    LEFT JOIN IAM.[User] ua ON ua.UserId = r.ApprovedByUserId
    WHERE r.TenantId = @TenantId AND r.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%' OR ro.RoleName LIKE '%' + @SearchTerm + '%' OR r.StatusCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.PrivilegedAccessRequest r
JOIN IAM.[User] u ON u.UserId = r.RequestedByUserId
JOIN IAM.Role ro ON ro.RoleId = r.TargetRoleId
WHERE r.TenantId = @TenantId AND r.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%' OR ro.RoleName LIKE '%' + @SearchTerm + '%' OR r.StatusCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PrivilegedAccessRequestDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PrivilegedAccessRequestDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> SubmitAsync(SubmitPrivilegedAccessRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.PrivilegedAccessRequest (RequestId, TenantId, RequestedByUserId, TargetRoleId, JustificationText, RequestedStartDateUtc, RequestedEndDateUtc, StatusCode, CreatedDateUtc, IsDeleted)
VALUES (@RequestId, @TenantId, @RequestedByUserId, @TargetRoleId, @JustificationText, @RequestedStartDateUtc, @RequestedEndDateUtc, 'Pending', GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RequestId = id, request.TenantId, request.RequestedByUserId, request.TargetRoleId, request.JustificationText, request.RequestedStartDateUtc, request.RequestedEndDateUtc }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ReviewAsync(ReviewAccessDecisionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE IAM.PrivilegedAccessRequest
SET StatusCode = @StatusCode, ApprovedByUserId = @ReviewerUserId, ApprovalDateUtc = GETUTCDATE(),
    GrantedStartDateUtc = CASE WHEN @IsApproved = 1 THEN GETUTCDATE() ELSE NULL END,
    GrantedEndDateUtc = CASE WHEN @IsApproved = 1 THEN DATEADD(day, 1, GETUTCDATE()) ELSE NULL END,
    ModifiedDateUtc = GETUTCDATE()
WHERE RequestId = @RequestId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.RequestId, StatusCode = request.IsApproved ? "Approved" : "Rejected", request.ReviewerUserId, request.IsApproved }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAsync(Guid requestId, Guid revokedByUserId, string reason, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.PrivilegedAccessRequest SET StatusCode = 'Revoked', RevokedReason = @Reason, RevokedDateUtc = GETUTCDATE(), ModifiedByUserId = @RevokedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE RequestId = @RequestId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RequestId = requestId, RevokedByUserId = revokedByUserId, Reason = reason }, cancellationToken: cancellationToken));
    }
}
