using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Governance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccessReviewRepository : IAccessReviewRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AccessReviewRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<UserAccessReviewDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT rv.ReviewId, rv.TenantId, rv.ReviewCycleCode, rv.ReviewerUserId, ur.FullName AS ReviewerFullName,
       rv.SubjectUserId, us.FullName AS SubjectFullName, rv.RoleId, r.RoleName,
       rv.DecisionCode, rv.DecisionNotes, rv.ReviewedDateUtc, rv.DueByDateUtc, rv.StatusCode, rv.CreatedDateUtc
FROM IAM.UserAccessReview rv
JOIN IAM.[User] ur ON ur.UserId = rv.ReviewerUserId
JOIN IAM.[User] us ON us.UserId = rv.SubjectUserId
JOIN IAM.Role r ON r.RoleId = rv.RoleId
WHERE rv.ReviewId = @Id AND rv.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<UserAccessReviewDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<UserAccessReviewDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT rv.ReviewId, rv.TenantId, rv.ReviewCycleCode, rv.ReviewerUserId, ur.FullName AS ReviewerFullName,
           rv.SubjectUserId, us.FullName AS SubjectFullName, rv.RoleId, r.RoleName,
           rv.DecisionCode, rv.DecisionNotes, rv.ReviewedDateUtc, rv.DueByDateUtc, rv.StatusCode, rv.CreatedDateUtc
    FROM IAM.UserAccessReview rv
    JOIN IAM.[User] ur ON ur.UserId = rv.ReviewerUserId
    JOIN IAM.[User] us ON us.UserId = rv.SubjectUserId
    JOIN IAM.Role r ON r.RoleId = rv.RoleId
    WHERE rv.TenantId = @TenantId AND rv.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR us.FullName LIKE '%' + @SearchTerm + '%' OR r.RoleName LIKE '%' + @SearchTerm + '%' OR rv.StatusCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY DueByDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.UserAccessReview rv
JOIN IAM.[User] us ON us.UserId = rv.SubjectUserId
JOIN IAM.Role r ON r.RoleId = rv.RoleId
WHERE rv.TenantId = @TenantId AND rv.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR us.FullName LIKE '%' + @SearchTerm + '%' OR r.RoleName LIKE '%' + @SearchTerm + '%' OR rv.StatusCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<UserAccessReviewDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<UserAccessReviewDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    // ── Campaigns ──────────────────────────────────────────────────────────────

    public async Task<AccessReviewCampaignDto?> GetCampaignByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT c.CampaignId, c.TenantId, c.CampaignName, c.Description, c.ScopeTypeCode,
       c.ScopeReferenceId,
       CASE WHEN c.ScopeTypeCode = 'ByRole' THEN r.RoleName ELSE NULL END AS ScopeReferenceName,
       c.ReviewerUserId, ur.FullName AS ReviewerFullName,
       c.StartDateUtc, c.EndDateUtc, c.StatusCode,
       (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0) AS TotalItemCount,
       (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode IS NOT NULL) AS ReviewedItemCount,
       (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode = 'Keep') AS KeepCount,
       (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode = 'Remove') AS RemoveCount,
       (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode = 'Escalate') AS EscalateCount,
       c.CreatedByUserId, uc.FullName AS CreatedByFullName, c.CreatedDateUtc, c.ModifiedDateUtc
FROM IAM.AccessReviewCampaign c
JOIN IAM.[User] ur ON ur.UserId = c.ReviewerUserId
JOIN IAM.[User] uc ON uc.UserId = c.CreatedByUserId
LEFT JOIN IAM.Role r ON r.RoleId = c.ScopeReferenceId AND c.ScopeTypeCode = 'ByRole'
WHERE c.CampaignId = @Id AND c.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<AccessReviewCampaignDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<AccessReviewCampaignDto>> SearchCampaignsAsync(Guid tenantId, string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT c.CampaignId, c.TenantId, c.CampaignName, c.Description, c.ScopeTypeCode,
           c.ScopeReferenceId,
           CASE WHEN c.ScopeTypeCode = 'ByRole' THEN r.RoleName ELSE NULL END AS ScopeReferenceName,
           c.ReviewerUserId, ur.FullName AS ReviewerFullName,
           c.StartDateUtc, c.EndDateUtc, c.StatusCode,
           (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0) AS TotalItemCount,
           (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode IS NOT NULL) AS ReviewedItemCount,
           (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode = 'Keep') AS KeepCount,
           (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode = 'Remove') AS RemoveCount,
           (SELECT COUNT(1) FROM IAM.AccessReviewItem i WHERE i.CampaignId = c.CampaignId AND i.IsDeleted = 0 AND i.DecisionCode = 'Escalate') AS EscalateCount,
           c.CreatedByUserId, uc.FullName AS CreatedByFullName, c.CreatedDateUtc, c.ModifiedDateUtc
    FROM IAM.AccessReviewCampaign c
    JOIN IAM.[User] ur ON ur.UserId = c.ReviewerUserId
    JOIN IAM.[User] uc ON uc.UserId = c.CreatedByUserId
    LEFT JOIN IAM.Role r ON r.RoleId = c.ScopeReferenceId AND c.ScopeTypeCode = 'ByRole'
    WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
      AND (@StatusCode IS NULL OR @StatusCode = '' OR c.StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR c.CampaignName LIKE '%' + @SearchTerm + '%' OR ur.FullName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.AccessReviewCampaign c
WHERE c.TenantId = @TenantId AND c.IsDeleted = 0
  AND (@StatusCode IS NULL OR @StatusCode = '' OR c.StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR c.CampaignName LIKE '%' + @SearchTerm + '%');";
        var p = new { TenantId = tenantId, SearchTerm = searchTerm, StatusCode = statusCode, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize };
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        var campaigns = (await multi.ReadAsync<AccessReviewCampaignDto>()).AsList();
        var total     = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccessReviewCampaignDto> { Items = campaigns, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateCampaignAsync(CreateAccessReviewCampaignRequest request, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO IAM.AccessReviewCampaign
    (CampaignId, TenantId, CampaignName, Description, ScopeTypeCode, ScopeReferenceId,
     ReviewerUserId, StartDateUtc, EndDateUtc, StatusCode, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
    (@CampaignId, @TenantId, @CampaignName, @Description, @ScopeTypeCode, @ScopeReferenceId,
     @ReviewerUserId, @StartDateUtc, @EndDateUtc, 'Draft', @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CampaignId       = id,
            request.TenantId,
            request.CampaignName,
            request.Description,
            request.ScopeTypeCode,
            request.ScopeReferenceId,
            request.ReviewerUserId,
            request.StartDateUtc,
            request.EndDateUtc,
            request.CreatedByUserId,
        }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateCampaignAsync(Guid id, UpdateAccessReviewCampaignRequest request, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE IAM.AccessReviewCampaign SET
    CampaignName     = @CampaignName,
    Description      = @Description,
    ScopeTypeCode    = @ScopeTypeCode,
    ScopeReferenceId = @ScopeReferenceId,
    ReviewerUserId   = @ReviewerUserId,
    StartDateUtc     = @StartDateUtc,
    EndDateUtc       = @EndDateUtc,
    ModifiedDateUtc  = GETUTCDATE()
WHERE CampaignId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.CampaignName,
            request.Description,
            request.ScopeTypeCode,
            request.ScopeReferenceId,
            request.ReviewerUserId,
            request.StartDateUtc,
            request.EndDateUtc,
        }, cancellationToken: ct));
    }

    public async Task ChangeCampaignStatusAsync(Guid id, string newStatusCode, Guid changedByUserId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE IAM.AccessReviewCampaign SET
    StatusCode      = @StatusCode,
    ModifiedDateUtc = GETUTCDATE()
WHERE CampaignId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, StatusCode = newStatusCode }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AccessReviewItemDto>> GetItemsAsync(Guid campaignId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT i.ReviewItemId, i.CampaignId, i.UserId, u.FullName AS UserFullName, u.Email AS UserEmail,
       i.AccessTypeCode, i.AccessReferenceId, i.AccessName, i.RiskLevel,
       i.DecisionCode, i.ReviewerNotes, i.ReviewedByUserId, rv.FullName AS ReviewedByFullName,
       i.ReviewedDateUtc, i.CreatedDateUtc
FROM IAM.AccessReviewItem i
JOIN IAM.[User] u  ON u.UserId  = i.UserId
LEFT JOIN IAM.[User] rv ON rv.UserId = i.ReviewedByUserId
WHERE i.CampaignId = @CampaignId AND i.IsDeleted = 0
ORDER BY u.FullName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<AccessReviewItemDto>(new CommandDefinition(sql, new { CampaignId = campaignId }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task SubmitDecisionAsync(Guid campaignId, Guid itemId, SubmitReviewDecisionRequest request, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE IAM.AccessReviewItem SET
    DecisionCode     = @DecisionCode,
    ReviewerNotes    = @ReviewerNotes,
    ReviewedByUserId = @ReviewedByUserId,
    ReviewedDateUtc  = GETUTCDATE()
WHERE ReviewItemId = @ItemId AND CampaignId = @CampaignId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ItemId           = itemId,
            CampaignId       = campaignId,
            request.DecisionCode,
            request.ReviewerNotes,
            request.ReviewedByUserId,
        }, cancellationToken: ct));
    }
}
