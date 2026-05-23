using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountSegments;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountSegmentRuleRepository : IAccountSegmentRuleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountSegmentRuleRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO CRM.SegmentationRule
(
    RuleId, TenantId, SegmentId, SegmentCode, RuleCode, RuleName, Description,
    CriteriaJson, LogicConnector, Priority, RunOnSchedule, AccountsMatched,
    AccuracyPercent, LastRunDateUtc, IsActive, CreatedDateUtc, CreatedByUserId,
    ModifiedDateUtc, ModifiedByUserId, IsDeleted
)
VALUES
(
    @RuleId, @TenantId, @SegmentId, @SegmentCode, @RuleCode, @RuleName, @Description,
    @CriteriaJson, @LogicConnector, @Priority, @RunOnSchedule, 0,
    0, NULL, @IsActive, SYSUTCDATETIME(), @CreatedByUserId,
    NULL, NULL, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RuleId = id,
            request.TenantId,
            request.SegmentId,
            SegmentCode = request.SegmentCode.Trim(),
            RuleCode = request.RuleCode.Trim(),
            RuleName = request.RuleName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CriteriaJson = string.IsNullOrWhiteSpace(request.CriteriaJson) ? "[]" : request.CriteriaJson.Trim(),
            LogicConnector = string.IsNullOrWhiteSpace(request.LogicConnector) ? "AND" : request.LogicConnector.Trim().ToUpperInvariant(),
            request.Priority,
            request.RunOnSchedule,
            request.IsActive,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        await RecalculateAsync(request.TenantId, id, request.CreatedByUserId, cancellationToken);
        return id;
    }

    public async Task<AccountSegmentRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT r.RuleId, r.TenantId, r.SegmentId, r.SegmentCode,
       COALESCE(s.SegmentName, r.SegmentCode) AS SegmentName,
       r.RuleCode, r.RuleName, r.Description, r.CriteriaJson, r.LogicConnector,
       r.Priority, r.RunOnSchedule, r.AccountsMatched, r.AccuracyPercent,
       r.LastRunDateUtc, r.IsActive, r.CreatedDateUtc, r.ModifiedDateUtc,
       r.CreatedByUserId, r.ModifiedByUserId
FROM CRM.SegmentationRule r
LEFT JOIN Client.AccountSegment s
    ON s.TenantId = r.TenantId
   AND s.SegmentCode = r.SegmentCode
   AND s.IsDeleted = 0
WHERE r.RuleId = @Id AND r.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountSegmentRuleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountSegmentRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT r.RuleId, r.TenantId, r.SegmentId, r.SegmentCode,
           COALESCE(s.SegmentName, r.SegmentCode) AS SegmentName,
           r.RuleCode, r.RuleName, r.Description, r.CriteriaJson, r.LogicConnector,
           r.Priority, r.RunOnSchedule, r.AccountsMatched, r.AccuracyPercent,
           r.LastRunDateUtc, r.IsActive, r.CreatedDateUtc, r.ModifiedDateUtc,
           r.CreatedByUserId, r.ModifiedByUserId
    FROM CRM.SegmentationRule r
    LEFT JOIN Client.AccountSegment s
        ON s.TenantId = r.TenantId
       AND s.SegmentCode = r.SegmentCode
       AND s.IsDeleted = 0
    WHERE r.TenantId = @TenantId
      AND r.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR r.RuleName LIKE '%' + @SearchTerm + '%'
           OR r.RuleCode LIKE '%' + @SearchTerm + '%'
           OR r.SegmentCode LIKE '%' + @SearchTerm + '%'
           OR s.SegmentName LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY Priority ASC, CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(*)
FROM CRM.SegmentationRule r
LEFT JOIN Client.AccountSegment s
    ON s.TenantId = r.TenantId
   AND s.SegmentCode = r.SegmentCode
   AND s.IsDeleted = 0
WHERE r.TenantId = @TenantId
  AND r.IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR r.RuleName LIKE '%' + @SearchTerm + '%'
       OR r.RuleCode LIKE '%' + @SearchTerm + '%'
       OR r.SegmentCode LIKE '%' + @SearchTerm + '%'
       OR s.SegmentName LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AccountSegmentRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AccountSegmentRuleDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(Guid id, UpdateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.SegmentationRule
SET SegmentId = @SegmentId,
    SegmentCode = @SegmentCode,
    RuleCode = @RuleCode,
    RuleName = @RuleName,
    Description = @Description,
    CriteriaJson = @CriteriaJson,
    LogicConnector = @LogicConnector,
    Priority = @Priority,
    RunOnSchedule = @RunOnSchedule,
    IsActive = @IsActive,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE RuleId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.SegmentId,
            SegmentCode = request.SegmentCode.Trim(),
            RuleCode = request.RuleCode.Trim(),
            RuleName = request.RuleName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CriteriaJson = string.IsNullOrWhiteSpace(request.CriteriaJson) ? "[]" : request.CriteriaJson.Trim(),
            LogicConnector = string.IsNullOrWhiteSpace(request.LogicConnector) ? "AND" : request.LogicConnector.Trim().ToUpperInvariant(),
            request.Priority,
            request.RunOnSchedule,
            request.IsActive,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));

        var tenantId = await GetTenantIdAsync(id, cancellationToken);
        if (tenantId.HasValue)
        {
            await RecalculateAsync(tenantId.Value, id, request.ModifiedByUserId, cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.SegmentationRule
SET IsDeleted = 1,
    IsActive = 0,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE RuleId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task RecalculateAsync(Guid tenantId, Guid? id = null, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE r
SET AccountsMatched = x.AccountsMatched,
    AccuracyPercent = CASE
        WHEN x.AccountsMatched >= 100 THEN 94
        WHEN x.AccountsMatched >= 25 THEN 88
        WHEN x.AccountsMatched > 0 THEN 81
        ELSE 0
    END,
    LastRunDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM CRM.SegmentationRule r
OUTER APPLY (
    SELECT COUNT(1) AS AccountsMatched
    FROM Client.Account a
    WHERE a.TenantId = r.TenantId
      AND a.IsDeleted = 0
      AND a.SegmentCode = r.SegmentCode
) x
WHERE r.TenantId = @TenantId
  AND r.IsDeleted = 0
  AND (@Id IS NULL OR r.RuleId = @Id);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task<Guid?> GetTenantIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TenantId FROM CRM.SegmentationRule WHERE RuleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
