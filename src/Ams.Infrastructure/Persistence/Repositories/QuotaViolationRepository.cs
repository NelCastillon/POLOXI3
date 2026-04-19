using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.QuotaViolations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class QuotaViolationRepository : IQuotaViolationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public QuotaViolationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<QuotaViolationDto>> SearchAsync(string? searchTerm, string? statusCode, string? severityCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT v.ViolationId, v.TenantId, ISNULL(t.TenantName, '') AS TenantName,
           v.MetricTypeCode, v.ViolationDateUtc, v.LimitValue, v.ActualValue, v.ExcessValue,
           v.SeverityCode, v.StatusCode, v.Notes,
           v.AcknowledgedByUserId, v.AcknowledgedDateUtc,
           v.ResolvedByUserId, v.ResolvedDateUtc, v.CreatedDateUtc
    FROM Core.QuotaViolation v
    LEFT JOIN Core.Tenant t ON t.TenantId = v.TenantId
    WHERE v.IsDeleted = 0
      AND (@StatusCode   IS NULL OR @StatusCode   = '' OR v.StatusCode   = @StatusCode)
      AND (@SeverityCode IS NULL OR @SeverityCode = '' OR v.SeverityCode = @SeverityCode)
      AND (@SearchTerm   IS NULL OR @SearchTerm   = ''
           OR t.TenantName      LIKE '%' + @SearchTerm + '%'
           OR v.MetricTypeCode  LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT v.ViolationId, v.TenantId, ISNULL(t.TenantName, '') AS TenantName,
           v.MetricTypeCode, v.ViolationDateUtc, v.LimitValue, v.ActualValue, v.ExcessValue,
           v.SeverityCode, v.StatusCode, v.Notes,
           v.AcknowledgedByUserId, v.AcknowledgedDateUtc,
           v.ResolvedByUserId, v.ResolvedDateUtc, v.CreatedDateUtc
    FROM Core.QuotaViolation v
    LEFT JOIN Core.Tenant t ON t.TenantId = v.TenantId
    WHERE v.IsDeleted = 0
      AND (@StatusCode   IS NULL OR @StatusCode   = '' OR v.StatusCode   = @StatusCode)
      AND (@SeverityCode IS NULL OR @SeverityCode = '' OR v.SeverityCode = @SeverityCode)
      AND (@SearchTerm   IS NULL OR @SearchTerm   = ''
           OR t.TenantName      LIKE '%' + @SearchTerm + '%'
           OR v.MetricTypeCode  LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY ViolationDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm   = searchTerm,
            StatusCode   = statusCode,
            SeverityCode = severityCode,
            PageNumber   = pageNumber,
            PageSize     = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<QuotaViolationDto>()).ToList();
        return new PagedResult<QuotaViolationDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<QuotaViolationDto?> GetByIdAsync(Guid violationId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT v.ViolationId, v.TenantId, ISNULL(t.TenantName, '') AS TenantName,
       v.MetricTypeCode, v.ViolationDateUtc, v.LimitValue, v.ActualValue, v.ExcessValue,
       v.SeverityCode, v.StatusCode, v.Notes,
       v.AcknowledgedByUserId, v.AcknowledgedDateUtc,
       v.ResolvedByUserId, v.ResolvedDateUtc, v.CreatedDateUtc
FROM Core.QuotaViolation v
LEFT JOIN Core.Tenant t ON t.TenantId = v.TenantId
WHERE v.ViolationId = @ViolationId AND v.IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<QuotaViolationDto>(sql, new { ViolationId = violationId });
    }

    public async Task AcknowledgeAsync(Guid violationId, AcknowledgeQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaViolation
SET StatusCode           = 'Acknowledged',
    AcknowledgedByUserId = @AcknowledgedByUserId,
    AcknowledgedDateUtc  = SYSUTCDATETIME(),
    Notes                = ISNULL(@Notes, Notes)
WHERE ViolationId = @ViolationId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            ViolationId          = violationId,
            request.AcknowledgedByUserId,
            request.Notes
        });
    }

    public async Task ResolveAsync(Guid violationId, ResolveQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaViolation
SET StatusCode       = 'Resolved',
    ResolvedByUserId = @ResolvedByUserId,
    ResolvedDateUtc  = SYSUTCDATETIME(),
    Notes            = ISNULL(@Notes, Notes)
WHERE ViolationId = @ViolationId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            ViolationId      = violationId,
            request.ResolvedByUserId,
            request.Notes
        });
    }

    public async Task NotifyAsync(Guid violationId, NotifyQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaViolation
SET StatusCode = 'Notified',
    Notes      = ISNULL(@Notes, Notes)
WHERE ViolationId = @ViolationId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { ViolationId = violationId, request.Notes });
    }

    public async Task ApplyRestrictionAsync(Guid violationId, ApplyRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaViolation
SET StatusCode = 'Restricted',
    Notes      = ISNULL(@Notes, Notes)
WHERE ViolationId = @ViolationId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { ViolationId = violationId, request.Notes });
    }

    public async Task GrantTemporaryIncreaseAsync(Guid violationId, GrantTemporaryIncreaseRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaViolation
SET StatusCode = 'TemporaryIncrease',
    Notes      = ISNULL(@Notes, Notes)
WHERE ViolationId = @ViolationId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { ViolationId = violationId, request.Notes });
    }

    public async Task ConvertToOverageAsync(Guid violationId, ConvertToOverageRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.QuotaViolation
SET StatusCode = 'BillingOverage',
    Notes      = ISNULL(@Notes, Notes)
WHERE ViolationId = @ViolationId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { ViolationId = violationId, request.Notes });
    }

    public async Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM Core.QuotaViolation WHERE StatusCode = 'Open' AND IsDeleted = 0;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(sql);
    }
}
