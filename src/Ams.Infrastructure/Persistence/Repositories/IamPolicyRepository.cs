using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class IamPolicyRepository : IIamPolicyRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public IamPolicyRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<FieldSecurityPolicyDto?> GetFieldPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.PolicyId, p.TenantId, p.RoleId, r.RoleName, p.EntityName, p.FieldName, p.CanRead, p.CanWrite, p.IsHidden, p.CreatedDateUtc, p.ModifiedDateUtc
FROM IAM.FieldSecurityPolicy p JOIN IAM.Role r ON r.RoleId = p.RoleId
WHERE p.PolicyId = @Id AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<FieldSecurityPolicyDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<FieldSecurityPolicyDto>> SearchFieldPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT p.PolicyId, p.TenantId, p.RoleId, r.RoleName, p.EntityName, p.FieldName, p.CanRead, p.CanWrite, p.IsHidden, p.CreatedDateUtc, p.ModifiedDateUtc
    FROM IAM.FieldSecurityPolicy p JOIN IAM.Role r ON r.RoleId = p.RoleId
    WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR p.EntityName LIKE '%' + @SearchTerm + '%' OR p.FieldName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY EntityName, FieldName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.FieldSecurityPolicy p WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR p.EntityName LIKE '%' + @SearchTerm + '%' OR p.FieldName LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<FieldSecurityPolicyDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<FieldSecurityPolicyDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<RecordSecurityPolicyDto?> GetRecordPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.PolicyId, p.TenantId, p.RoleId, r.RoleName, p.EntityName, p.PolicyTypeCode, p.FilterExpression, p.IsActive, p.CreatedDateUtc, p.ModifiedDateUtc
FROM IAM.RecordSecurityPolicy p JOIN IAM.Role r ON r.RoleId = p.RoleId
WHERE p.PolicyId = @Id AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RecordSecurityPolicyDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RecordSecurityPolicyDto>> SearchRecordPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT p.PolicyId, p.TenantId, p.RoleId, r.RoleName, p.EntityName, p.PolicyTypeCode, p.FilterExpression, p.IsActive, p.CreatedDateUtc, p.ModifiedDateUtc
    FROM IAM.RecordSecurityPolicy p JOIN IAM.Role r ON r.RoleId = p.RoleId
    WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR p.EntityName LIKE '%' + @SearchTerm + '%' OR p.PolicyTypeCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY EntityName, PolicyTypeCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.RecordSecurityPolicy p WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR p.EntityName LIKE '%' + @SearchTerm + '%' OR p.PolicyTypeCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RecordSecurityPolicyDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RecordSecurityPolicyDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateFieldPolicyAsync(CreateFieldSecurityPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.FieldSecurityPolicy (PolicyId, TenantId, RoleId, EntityName, FieldName, CanRead, CanWrite, IsHidden, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@PolicyId, @TenantId, @RoleId, @EntityName, @FieldName, @CanRead, @CanWrite, @IsHidden, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyId = id, request.TenantId, request.RoleId, request.EntityName, request.FieldName, request.CanRead, request.CanWrite, request.IsHidden, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeleteFieldPolicyAsync(Guid policyId, Guid? deletedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.FieldSecurityPolicy SET IsDeleted = 1, ModifiedByUserId = @DeletedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE PolicyId = @PolicyId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyId = policyId, DeletedByUserId = deletedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateRecordPolicyAsync(CreateRecordSecurityPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.RecordSecurityPolicy (PolicyId, TenantId, RoleId, EntityName, PolicyTypeCode, FilterExpression, IsActive, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@PolicyId, @TenantId, @RoleId, @EntityName, @PolicyTypeCode, @FilterExpression, 1, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyId = id, request.TenantId, request.RoleId, request.EntityName, request.PolicyTypeCode, request.FilterExpression, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeleteRecordPolicyAsync(Guid policyId, Guid? deletedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.RecordSecurityPolicy SET IsDeleted = 1, ModifiedByUserId = @DeletedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE PolicyId = @PolicyId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyId = policyId, DeletedByUserId = deletedByUserId }, cancellationToken: cancellationToken));
    }
}
