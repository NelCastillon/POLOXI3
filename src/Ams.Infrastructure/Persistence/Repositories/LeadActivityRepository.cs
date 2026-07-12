using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.LeadActivities;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class LeadActivityRepository : ILeadActivityRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LeadActivityRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateLeadActivityRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.LeadId.HasValue && !request.OpportunityId.HasValue)
        {
            throw new InvalidOperationException("A lead or opportunity is required to log an activity.");
        }

        if (string.IsNullOrWhiteSpace(request.ActivityTypeCode))
        {
            throw new InvalidOperationException("Activity type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new InvalidOperationException("Activity subject is required.");
        }

        const string sql = @"
INSERT INTO CRM.LeadActivity
(
    ActivityId, TenantId, LeadId, OpportunityId, ActivityTypeCode,
    Subject, Notes, ActivityDate, DurationMinutes, OutcomeCode,
    IsCompleted, CreatedByUserId, CreatedDateUtc, IsDeleted
)
VALUES
(
    @ActivityId, @TenantId, @LeadId, @OpportunityId, @ActivityTypeCode,
    @Subject, @Notes, @ActivityDate, @DurationMinutes, @OutcomeCode,
    0, @CreatedByUserId, SYSUTCDATETIME(), 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ActivityId = id,
            request.TenantId,
            request.LeadId,
            request.OpportunityId,
            ActivityTypeCode = request.ActivityTypeCode.Trim(),
            Subject = request.Subject.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            request.ActivityDate,
            request.DurationMinutes,
            request.OutcomeCode,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task UpdateAsync(UpdateLeadActivityRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.LeadId.HasValue && !request.OpportunityId.HasValue)
        {
            throw new InvalidOperationException("A lead or opportunity is required to update an activity.");
        }

        if (string.IsNullOrWhiteSpace(request.ActivityTypeCode))
        {
            throw new InvalidOperationException("Activity type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new InvalidOperationException("Activity subject is required.");
        }

        const string sql = @"
UPDATE CRM.LeadActivity
SET LeadId = @LeadId,
    OpportunityId = @OpportunityId,
    ActivityTypeCode = @ActivityTypeCode,
    Subject = @Subject,
    Notes = @Notes,
    ActivityDate = @ActivityDate,
    DurationMinutes = @DurationMinutes,
    OutcomeCode = @OutcomeCode,
    IsCompleted = @IsCompleted,
    ModifiedByUserId = @ModifiedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ActivityId = @ActivityId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.ActivityId,
            request.LeadId,
            request.OpportunityId,
            ActivityTypeCode = request.ActivityTypeCode.Trim(),
            Subject = request.Subject.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            request.ActivityDate,
            request.DurationMinutes,
            request.OutcomeCode,
            request.IsCompleted,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.LeadActivity
SET IsDeleted = 1,
    ModifiedByUserId = @ModifiedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ActivityId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LeadActivityDto>> GetByLeadIdAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT a.ActivityId, a.TenantId, a.LeadId,
       ISNULL(l.FirstName + ' ' + l.LastName, '') AS LeadName,
       l.AssignedToUserId AS ProducerUserId,
       COALESCE(u.DisplayName, u.FullName, u.Email, CONVERT(NVARCHAR(36), l.AssignedToUserId)) AS ProducerName,
       a.OpportunityId, o.OpportunityName,
       a.ActivityTypeCode, a.Subject, a.Notes, a.ActivityDate,
       a.DurationMinutes, a.OutcomeCode, a.IsCompleted, a.CreatedDateUtc
FROM CRM.LeadActivity a
LEFT JOIN CRM.Lead l ON l.LeadId = a.LeadId
LEFT JOIN IAM.[User] u ON u.UserId = l.AssignedToUserId
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = a.OpportunityId
WHERE a.LeadId = @LeadId AND a.IsDeleted = 0
ORDER BY a.ActivityDate DESC, a.CreatedDateUtc DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<LeadActivityDto>(new CommandDefinition(sql, new { LeadId = leadId }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<LeadActivityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT a.ActivityId, a.TenantId, a.LeadId,
       ISNULL(l.FirstName + ' ' + l.LastName, '') AS LeadName,
       l.AssignedToUserId AS ProducerUserId,
       COALESCE(u.DisplayName, u.FullName, u.Email, CONVERT(NVARCHAR(36), l.AssignedToUserId)) AS ProducerName,
       a.OpportunityId, o.OpportunityName,
       a.ActivityTypeCode, a.Subject, a.Notes, a.ActivityDate,
       a.DurationMinutes, a.OutcomeCode, a.IsCompleted, a.CreatedDateUtc
FROM CRM.LeadActivity a
LEFT JOIN CRM.Lead l ON l.LeadId = a.LeadId
LEFT JOIN IAM.[User] u ON u.UserId = l.AssignedToUserId
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = a.OpportunityId
WHERE a.ActivityId = @Id AND a.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<LeadActivityDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LeadActivityOutcomeDto>> GetOutcomesAsync(Guid tenantId, string? activityTypeCode = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ActivityOutcomeId, TenantId, ActivityTypeCode, OutcomeCode, OutcomeName, Description, SortOrder, IsActive
FROM CRM.LeadActivityOutcome
WHERE TenantId = @TenantId
  AND IsActive = 1
  AND IsDeleted = 0
  AND (@ActivityTypeCode IS NULL OR @ActivityTypeCode = '' OR ActivityTypeCode = @ActivityTypeCode)
ORDER BY ActivityTypeCode, SortOrder, OutcomeName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<LeadActivityOutcomeDto>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            ActivityTypeCode = string.IsNullOrWhiteSpace(activityTypeCode) ? null : activityTypeCode.Trim()
        }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<PagedResult<LeadActivityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT a.ActivityId, a.TenantId, a.LeadId,
           ISNULL(l.FirstName + ' ' + l.LastName, '') AS LeadName,
           l.AssignedToUserId AS ProducerUserId,
           COALESCE(u.DisplayName, u.FullName, u.Email, CONVERT(NVARCHAR(36), l.AssignedToUserId)) AS ProducerName,
           a.OpportunityId, o.OpportunityName,
           a.ActivityTypeCode, a.Subject, a.Notes, a.ActivityDate,
           a.DurationMinutes, a.OutcomeCode, a.IsCompleted, a.CreatedDateUtc
    FROM CRM.LeadActivity a
    LEFT JOIN CRM.Lead l ON l.LeadId = a.LeadId
    LEFT JOIN IAM.[User] u ON u.UserId = l.AssignedToUserId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = a.OpportunityId
    WHERE a.TenantId = @TenantId AND a.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR a.Subject LIKE '%' + @SearchTerm + '%'
           OR a.ActivityTypeCode LIKE '%' + @SearchTerm + '%'
           OR l.FirstName LIKE '%' + @SearchTerm + '%'
           OR l.LastName LIKE '%' + @SearchTerm + '%'
      )
)
SELECT * FROM Paged ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM CRM.LeadActivity WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<LeadActivityDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<LeadActivityDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
