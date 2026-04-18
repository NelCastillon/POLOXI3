using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Sod;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SodRuleRepository : ISodRuleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SodRuleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        s.SodRuleId, s.TenantId, s.RuleCode, s.RuleName, s.Description,
        s.RoleAId, ra.RoleName AS RoleAName,
        s.RoleBId, rb.RoleName AS RoleBName,
        s.PermissionAId, pa.PermissionName AS PermissionAName,
        s.PermissionBId, pb.PermissionName AS PermissionBName,
        s.SeverityCode, s.Reason, s.ExceptionPolicyCode,
        s.IsActive, s.IsSystemDefined, s.CreatedDateUtc, s.ModifiedDateUtc";

    private const string Joins = @"
        JOIN IAM.Role ra ON ra.RoleId = s.RoleAId
        JOIN IAM.Role rb ON rb.RoleId = s.RoleBId
        LEFT JOIN IAM.Permission pa ON pa.PermissionId = s.PermissionAId
        LEFT JOIN IAM.Permission pb ON pb.PermissionId = s.PermissionBId";

    public async Task<SegregationOfDutyRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM IAM.SegregationOfDutyRule s {Joins}
WHERE s.SodRuleId = @Id AND s.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SegregationOfDutyRuleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<SegregationOfDutyRuleDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? severityCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {SelectColumns}
    FROM IAM.SegregationOfDutyRule s {Joins}
    WHERE s.IsDeleted = 0
      AND (@TenantId     IS NULL OR s.TenantId IS NULL OR s.TenantId = @TenantId)
      AND (@SeverityCode IS NULL OR @SeverityCode = '' OR s.SeverityCode = @SeverityCode)
      AND (@IsActive     IS NULL OR s.IsActive = @IsActive)
      AND (@SearchTerm   IS NULL OR @SearchTerm = ''
           OR s.RuleName LIKE '%' + @SearchTerm + '%'
           OR s.RuleCode LIKE '%' + @SearchTerm + '%'
           OR ra.RoleName LIKE '%' + @SearchTerm + '%'
           OR rb.RoleName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY RuleName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM IAM.SegregationOfDutyRule s {Joins}
WHERE s.IsDeleted = 0
  AND (@TenantId     IS NULL OR s.TenantId IS NULL OR s.TenantId = @TenantId)
  AND (@SeverityCode IS NULL OR @SeverityCode = '' OR s.SeverityCode = @SeverityCode)
  AND (@IsActive     IS NULL OR s.IsActive = @IsActive)
  AND (@SearchTerm   IS NULL OR @SearchTerm = ''
       OR s.RuleName LIKE '%' + @SearchTerm + '%'
       OR s.RuleCode LIKE '%' + @SearchTerm + '%'
       OR ra.RoleName LIKE '%' + @SearchTerm + '%'
       OR rb.RoleName LIKE '%' + @SearchTerm + '%');";
        var p = new
        {
            TenantId     = tenantId,
            SearchTerm   = searchTerm,
            SeverityCode = severityCode,
            IsActive     = isActive,
            Offset       = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize     = pageSize,
        };
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, p, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SegregationOfDutyRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SegregationOfDutyRuleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO IAM.SegregationOfDutyRule
    (SodRuleId, TenantId, RuleCode, RuleName, Description,
     RoleAId, RoleBId, PermissionAId, PermissionBId,
     SeverityCode, Reason, ExceptionPolicyCode,
     IsActive, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
    (@SodRuleId, @TenantId, @RuleCode, @RuleName, @Description,
     @RoleAId, @RoleBId, @PermissionAId, @PermissionBId,
     @SeverityCode, @Reason, @ExceptionPolicyCode,
     1, 0, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SodRuleId          = id,
            request.TenantId,
            request.RuleCode,
            request.RuleName,
            request.Description,
            request.RoleAId,
            request.RoleBId,
            request.PermissionAId,
            request.PermissionBId,
            request.SeverityCode,
            request.Reason,
            request.ExceptionPolicyCode,
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.SegregationOfDutyRule SET
    RuleCode            = @RuleCode,
    RuleName            = @RuleName,
    Description         = @Description,
    RoleAId             = @RoleAId,
    RoleBId             = @RoleBId,
    PermissionAId       = @PermissionAId,
    PermissionBId       = @PermissionBId,
    SeverityCode        = @SeverityCode,
    Reason              = @Reason,
    ExceptionPolicyCode = @ExceptionPolicyCode,
    ModifiedDateUtc     = GETUTCDATE()
WHERE SodRuleId = @Id AND IsDeleted = 0 AND IsSystemDefined = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.RuleCode,
            request.RuleName,
            request.Description,
            request.RoleAId,
            request.RoleBId,
            request.PermissionAId,
            request.PermissionBId,
            request.SeverityCode,
            request.Reason,
            request.ExceptionPolicyCode,
        }, cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE IAM.SegregationOfDutyRule SET
    IsActive        = @IsActive,
    ModifiedDateUtc = GETUTCDATE()
WHERE SodRuleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, IsActive = isActive }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CloneAsync(Guid id, CloneSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var newId = Guid.NewGuid();
        const string sql = @"
INSERT INTO IAM.SegregationOfDutyRule
    (SodRuleId, TenantId, RuleCode, RuleName, Description,
     RoleAId, RoleBId, PermissionAId, PermissionBId,
     SeverityCode, Reason, ExceptionPolicyCode,
     IsActive, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT @NewId, TenantId, @NewRuleCode, @NewRuleName, Description,
       RoleAId, RoleBId, PermissionAId, PermissionBId,
       SeverityCode, Reason, ExceptionPolicyCode,
       0, 0, @ClonedByUserId, GETUTCDATE(), 0
FROM IAM.SegregationOfDutyRule
WHERE SodRuleId = @SourceId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            NewId          = newId,
            SourceId       = id,
            request.NewRuleCode,
            request.NewRuleName,
            request.ClonedByUserId,
        }, cancellationToken: cancellationToken));
        return newId;
    }
}
