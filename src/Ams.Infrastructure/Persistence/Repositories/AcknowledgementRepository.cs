using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AcknowledgementRepository : IAcknowledgementRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AcknowledgementRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ── Shared audience-pending fragment ────────────────────────────────────
    private const string PendingSelectColumns = @"
        p.PolicyDocumentId, p.PolicyCode, p.PolicyTitle, p.PolicyTypeCode, p.Version,
        p.EffectiveDateUtc, p.PublishedDateUtc,
        au.AudienceId, au.TargetTypeCode, au.TargetId AS TargetUserId, au.TargetName, au.IsRequired,
        DATEDIFF(day, COALESCE(p.EffectiveDateUtc, p.PublishedDateUtc), GETUTCDATE()) AS DaysOverdue";

    private const string PendingFromJoin = @"
FROM Compliance.PolicyDocument p
JOIN  Compliance.PolicyAudience au
    ON  au.PolicyDocumentId = p.PolicyDocumentId
    AND au.IsDeleted = 0
LEFT JOIN Compliance.PolicyAcknowledgement ack
    ON  ack.PolicyDocumentId = p.PolicyDocumentId
    AND au.TargetTypeCode    = 'User'
    AND ack.UserId           = au.TargetId";

    private const string PendingBaseWhere = @"
WHERE p.StatusCode  = 'Published'
  AND p.IsDeleted   = 0
  AND au.IsRequired = 1
  AND (au.TargetTypeCode != 'User' OR ack.AcknowledgementId IS NULL)
  AND (@TenantId   IS NULL OR p.TenantId = @TenantId)
  AND (@PolicyId   IS NULL OR p.PolicyDocumentId = @PolicyId)
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR p.PolicyCode  LIKE '%' + @SearchTerm + '%'
       OR p.PolicyTitle LIKE '%' + @SearchTerm + '%'
       OR au.TargetName LIKE '%' + @SearchTerm + '%')";

    public async Task<IReadOnlyList<PendingAcknowledgementDto>> GetPendingAsync(
        Guid? tenantId, Guid? policyId, string? searchTerm, CancellationToken ct = default)
    {
        var sql = $@"
SELECT {PendingSelectColumns}
{PendingFromJoin}
{PendingBaseWhere}
ORDER BY DaysOverdue DESC, p.PolicyCode, au.TargetName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<PendingAcknowledgementDto>(new CommandDefinition(sql, new
        {
            TenantId   = tenantId,
            PolicyId   = policyId,
            SearchTerm = searchTerm
        }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task<IReadOnlyList<PendingAcknowledgementDto>> GetOverdueAsync(
        Guid? tenantId, Guid? policyId, string? searchTerm, CancellationToken ct = default)
    {
        var sql = $@"
SELECT {PendingSelectColumns}
{PendingFromJoin}
{PendingBaseWhere}
  AND DATEDIFF(day, COALESCE(p.EffectiveDateUtc, p.PublishedDateUtc), GETUTCDATE()) > 0
ORDER BY DaysOverdue DESC, p.PolicyCode, au.TargetName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<PendingAcknowledgementDto>(new CommandDefinition(sql, new
        {
            TenantId   = tenantId,
            PolicyId   = policyId,
            SearchTerm = searchTerm
        }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task<PagedResult<AcknowledgementDetailDto>> SearchAcknowledgedAsync(
        Guid? tenantId, Guid? policyId, string? searchTerm,
        int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT a.AcknowledgementId, a.PolicyDocumentId,
           p.PolicyCode, p.PolicyTitle, p.PolicyTypeCode, p.Version, p.StatusCode,
           a.UserId, u.FullName AS UserFullName, u.Email AS UserEmail,
           a.AcknowledgedDateUtc, a.Channel, a.IpAddress
    FROM Compliance.PolicyAcknowledgement a
    JOIN Compliance.PolicyDocument p ON p.PolicyDocumentId = a.PolicyDocumentId AND p.IsDeleted = 0
    JOIN IAM.[User] u ON u.UserId = a.UserId
    WHERE (@TenantId  IS NULL OR p.TenantId          = @TenantId)
      AND (@PolicyId  IS NULL OR a.PolicyDocumentId  = @PolicyId)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR p.PolicyCode  LIKE '%' + @SearchTerm + '%'
           OR p.PolicyTitle LIKE '%' + @SearchTerm + '%'
           OR u.FullName    LIKE '%' + @SearchTerm + '%'
           OR u.Email       LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY AcknowledgedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Compliance.PolicyAcknowledgement a
JOIN Compliance.PolicyDocument p ON p.PolicyDocumentId = a.PolicyDocumentId AND p.IsDeleted = 0
JOIN IAM.[User] u ON u.UserId = a.UserId
WHERE (@TenantId  IS NULL OR p.TenantId         = @TenantId)
  AND (@PolicyId  IS NULL OR a.PolicyDocumentId = @PolicyId)
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR p.PolicyCode  LIKE '%' + @SearchTerm + '%'
       OR p.PolicyTitle LIKE '%' + @SearchTerm + '%'
       OR u.FullName    LIKE '%' + @SearchTerm + '%'
       OR u.Email       LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId   = tenantId,
            PolicyId   = policyId,
            SearchTerm = searchTerm,
            Offset     = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize   = pageSize
        }, cancellationToken: ct));

        var items = (await multi.ReadAsync<AcknowledgementDetailDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AcknowledgementDetailDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<AcknowledgementSummaryDto> GetSummaryAsync(Guid? tenantId, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
    (SELECT COUNT(DISTINCT p.PolicyDocumentId)
     FROM Compliance.PolicyDocument p
     JOIN Compliance.PolicyAudience au ON au.PolicyDocumentId = p.PolicyDocumentId AND au.IsDeleted = 0
     WHERE p.StatusCode = 'Published' AND p.IsDeleted = 0
       AND (@TenantId IS NULL OR p.TenantId = @TenantId)) AS TotalPoliciesWithAudience,

    (SELECT COUNT(1)
     FROM Compliance.PolicyDocument p
     JOIN  Compliance.PolicyAudience au
         ON  au.PolicyDocumentId = p.PolicyDocumentId AND au.IsDeleted = 0
     LEFT JOIN Compliance.PolicyAcknowledgement ack
         ON  ack.PolicyDocumentId = p.PolicyDocumentId
         AND au.TargetTypeCode    = 'User'
         AND ack.UserId           = au.TargetId
     WHERE p.StatusCode  = 'Published' AND p.IsDeleted = 0
       AND au.IsRequired = 1
       AND (au.TargetTypeCode != 'User' OR ack.AcknowledgementId IS NULL)
       AND (@TenantId IS NULL OR p.TenantId = @TenantId)) AS TotalPending,

    (SELECT COUNT(1)
     FROM Compliance.PolicyDocument p
     JOIN  Compliance.PolicyAudience au
         ON  au.PolicyDocumentId = p.PolicyDocumentId AND au.IsDeleted = 0
     LEFT JOIN Compliance.PolicyAcknowledgement ack
         ON  ack.PolicyDocumentId = p.PolicyDocumentId
         AND au.TargetTypeCode    = 'User'
         AND ack.UserId           = au.TargetId
     WHERE p.StatusCode  = 'Published' AND p.IsDeleted = 0
       AND au.IsRequired = 1
       AND (au.TargetTypeCode != 'User' OR ack.AcknowledgementId IS NULL)
       AND DATEDIFF(day, COALESCE(p.EffectiveDateUtc, p.PublishedDateUtc), GETUTCDATE()) > 0
       AND (@TenantId IS NULL OR p.TenantId = @TenantId)) AS TotalOverdue,

    (SELECT COUNT(1)
     FROM Compliance.PolicyAcknowledgement a
     JOIN Compliance.PolicyDocument p ON p.PolicyDocumentId = a.PolicyDocumentId AND p.IsDeleted = 0
     WHERE (@TenantId IS NULL OR p.TenantId = @TenantId)) AS TotalAcknowledged;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var result = await cn.QuerySingleAsync<AcknowledgementSummaryDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return result;
    }
}
