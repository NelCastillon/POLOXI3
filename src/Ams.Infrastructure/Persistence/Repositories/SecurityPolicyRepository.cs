using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SecurityPolicyRepository : ISecurityPolicyRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SecurityPolicyRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<SecurityPolicyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT SecurityPolicyId, TenantId, PolicyCode, PolicyName, Description, ResourceCode, ActionCode,
       ConditionExpression, SeverityCode, ErrorMessageTemplate, IsActive, IsSystemPolicy,
       CreatedDateUtc, ModifiedDateUtc
FROM IAM.SecurityPolicy
WHERE SecurityPolicyId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SecurityPolicyDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<SecurityPolicyDto?> GetByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT SecurityPolicyId, TenantId, PolicyCode, PolicyName, Description, ResourceCode, ActionCode,
       ConditionExpression, SeverityCode, ErrorMessageTemplate, IsActive, IsSystemPolicy,
       CreatedDateUtc, ModifiedDateUtc
FROM IAM.SecurityPolicy
WHERE TenantId = @TenantId AND PolicyCode = @PolicyCode AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SecurityPolicyDto>(new CommandDefinition(sql, new { TenantId = tenantId, PolicyCode = policyCode }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<SecurityPolicyDto>> SearchAsync(Guid tenantId, string? searchTerm, string? resourceCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT SecurityPolicyId, TenantId, PolicyCode, PolicyName, Description, ResourceCode, ActionCode,
           ConditionExpression, SeverityCode, ErrorMessageTemplate, IsActive, IsSystemPolicy,
           CreatedDateUtc, ModifiedDateUtc
    FROM IAM.SecurityPolicy
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR PolicyName LIKE '%' + @SearchTerm + '%' OR PolicyCode LIKE '%' + @SearchTerm + '%')
      AND (@ResourceCode IS NULL OR @ResourceCode = '' OR ResourceCode = @ResourceCode)
)
SELECT * FROM Cte ORDER BY ResourceCode, ActionCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.SecurityPolicy
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR PolicyName LIKE '%' + @SearchTerm + '%' OR PolicyCode LIKE '%' + @SearchTerm + '%')
  AND (@ResourceCode IS NULL OR @ResourceCode = '' OR ResourceCode = @ResourceCode);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, ResourceCode = resourceCode, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SecurityPolicyDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SecurityPolicyDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IEnumerable<SecurityPolicyDto>> GetActiveByResourceAsync(Guid tenantId, string resourceCode, string actionCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT SecurityPolicyId, TenantId, PolicyCode, PolicyName, Description, ResourceCode, ActionCode,
       ConditionExpression, SeverityCode, ErrorMessageTemplate, IsActive, IsSystemPolicy,
       CreatedDateUtc, ModifiedDateUtc
FROM IAM.SecurityPolicy
WHERE TenantId = @TenantId AND ResourceCode = @ResourceCode AND ActionCode = @ActionCode
  AND IsActive = 1 AND IsDeleted = 0
ORDER BY SeverityCode DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QueryAsync<SecurityPolicyDto>(new CommandDefinition(sql, new { TenantId = tenantId, ResourceCode = resourceCode, ActionCode = actionCode }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateSecurityPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO IAM.SecurityPolicy (SecurityPolicyId, TenantId, PolicyCode, PolicyName, Description, ResourceCode, ActionCode, ConditionExpression, SeverityCode, ErrorMessageTemplate, IsActive, IsSystemPolicy, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (@SecurityPolicyId, @TenantId, @PolicyCode, @PolicyName, @Description, @ResourceCode, @ActionCode, @ConditionExpression, @SeverityCode, @ErrorMessageTemplate, 1, 0, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SecurityPolicyId = id, request.TenantId, request.PolicyCode, request.PolicyName, request.Description, request.ResourceCode, request.ActionCode, request.ConditionExpression, request.SeverityCode, request.ErrorMessageTemplate, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeactivateAsync(Guid policyId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE IAM.SecurityPolicy SET IsActive = 0, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = GETUTCDATE() WHERE SecurityPolicyId = @PolicyId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyId = policyId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
