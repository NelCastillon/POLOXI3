using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RoleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT RoleId, TenantId, RoleCode, RoleName, RoleTypeCode, Description, SortOrder, IsBuiltIn, IsSystemRole, IsActive, CreatedDateUtc, ModifiedDateUtc FROM IAM.Role WHERE RoleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RoleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RoleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("IAM.Role", "RoleId, TenantId, RoleCode, RoleName, RoleTypeCode, Description, SortOrder, IsBuiltIn, IsSystemRole, IsActive, CreatedDateUtc, ModifiedDateUtc", "RoleName LIKE '%' + @SearchTerm + '%' OR RoleCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RoleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RoleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.Role (RoleId, TenantId, RoleCode, RoleName, RoleTypeCode, Description, SortOrder, IsBuiltIn, IsSystemRole, IsActive, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@RoleId, @TenantId, @RoleCode, @RoleName, @RoleTypeCode, @Description, @SortOrder, 0, @IsSystemRole, 1, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RoleId = id, request.TenantId, request.RoleCode, request.RoleName, request.RoleTypeCode, request.Description, request.SortOrder, request.IsSystemRole, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.Role SET RoleName = @RoleName, Description = @Description, SortOrder = @SortOrder, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE RoleId = @RoleId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.RoleId, request.RoleName, request.Description, request.SortOrder, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(Guid roleId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.Role SET IsActive = @IsActive, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE RoleId = @RoleId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RoleId = roleId, IsActive = isActive, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
