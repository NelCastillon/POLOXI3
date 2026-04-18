using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserGroupRepository : IUserGroupRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public UserGroupRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<UserGroupDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT UserGroupId, TenantId, GroupCode, GroupName, GroupTypeCode, Description, ManagerUserId, ParentGroupId, IsActive, CreatedDateUtc, ModifiedDateUtc FROM IAM.UserGroup WHERE UserGroupId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserGroupDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<UserGroupDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("IAM.UserGroup", "UserGroupId, TenantId, GroupCode, GroupName, GroupTypeCode, Description, ManagerUserId, ParentGroupId, IsActive, CreatedDateUtc, ModifiedDateUtc", "GroupName LIKE '%' + @SearchTerm + '%' OR GroupCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserGroupDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserGroupDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<UserGroupMemberDto>> SearchMembersAsync(Guid tenantId, Guid? userGroupId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT m.MemberId, m.TenantId, m.UserGroupId, g.GroupName, m.UserId, u.FullName AS UserFullName, m.JoinedDateUtc, m.RemovedDateUtc, m.IsActive, m.CreatedDateUtc
    FROM IAM.UserGroupMember m
    JOIN IAM.UserGroup g ON g.UserGroupId = m.UserGroupId
    JOIN IAM.[User] u ON u.UserId = m.UserId
    WHERE m.TenantId = @TenantId AND m.IsDeleted = 0
      AND (@UserGroupId IS NULL OR m.UserGroupId = @UserGroupId)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY JoinedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.UserGroupMember m
JOIN IAM.[User] u ON u.UserId = m.UserId
WHERE m.TenantId = @TenantId AND m.IsDeleted = 0
  AND (@UserGroupId IS NULL OR m.UserGroupId = @UserGroupId)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserGroupId = userGroupId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserGroupMemberDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserGroupMemberDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> AddMemberAsync(AddUserGroupMemberRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.UserGroupMember (MemberId, TenantId, UserGroupId, UserId, JoinedDateUtc, IsActive, AddedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@MemberId, @TenantId, @UserGroupId, @UserId, GETUTCDATE(), 1, @AddedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { MemberId = id, request.TenantId, request.UserGroupId, request.UserId, request.AddedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RemoveMemberAsync(Guid memberId, Guid? removedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.UserGroupMember SET IsActive = 0, RemovedDateUtc = GETUTCDATE(), ModifiedByUserId = @RemovedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE MemberId = @MemberId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { MemberId = memberId, RemovedByUserId = removedByUserId }, cancellationToken: cancellationToken));
    }
}
