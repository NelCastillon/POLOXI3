using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Sod;
using Ams.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SodConflictRepository : ISodConflictRepository
{
    private readonly ISqlConnectionFactory _db;

    public SodConflictRepository(ISqlConnectionFactory db) => _db = db;

    // ── Column list ────────────────────────────────────────────────────────────
    private const string SelectColumns = @"
        c.SodConflictId,
        c.TenantId,
        c.SodRuleId,
        r.RuleCode,
        r.RuleName,
        r.SeverityCode,
        c.UserId,
        u.FirstName + ' ' + u.LastName  AS UserFullName,
        u.Email                         AS UserEmail,
        c.DetectedDateUtc,
        c.StatusCode,
        CAST(CASE WHEN c.StatusCode = 'Resolved' THEN 1 ELSE 0 END AS BIT) AS IsResolved,
        c.ReviewerUserId,
        rv.FirstName + ' ' + rv.LastName AS ReviewerFullName,
        c.RemediationNote,
        c.ResolvedByUserId,
        rb.FirstName + ' ' + rb.LastName AS ResolvedByFullName,
        c.ResolutionNote,
        c.ResolvedDateUtc,
        c.CreatedDateUtc,
        c.ModifiedDateUtc";

    private const string Joins = @"
        JOIN  IAM.SegregationOfDutyRule r  ON r.SodRuleId       = c.SodRuleId
        JOIN  IAM.[User]                u  ON u.UserId           = c.UserId
        LEFT JOIN IAM.[User]            rv ON rv.UserId          = c.ReviewerUserId
        LEFT JOIN IAM.[User]            rb ON rb.UserId          = c.ResolvedByUserId";

    // ── GetById ────────────────────────────────────────────────────────────────
    public async Task<SodConflictDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var sql = $@"
            SELECT {SelectColumns}
            FROM   IAM.SodConflict c
            {Joins}
            WHERE  c.SodConflictId = @id
              AND  c.IsDeleted = 0";
        return await conn.QuerySingleOrDefaultAsync<SodConflictDto>(sql, new { id });
    }

    // ── Search ─────────────────────────────────────────────────────────────────
    public async Task<PagedResult<SodConflictDto>> SearchAsync(
        Guid?  tenantId, string? searchTerm, string? statusCode, string? severityCode,
        int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var where = new System.Text.StringBuilder("WHERE c.IsDeleted = 0");
        if (tenantId.HasValue)
            where.Append(" AND c.TenantId = @tenantId");
        if (!string.IsNullOrWhiteSpace(searchTerm))
            where.Append(" AND (u.FirstName + ' ' + u.LastName LIKE @search OR u.Email LIKE @search OR r.RuleCode LIKE @search OR r.RuleName LIKE @search)");
        if (!string.IsNullOrWhiteSpace(statusCode))
            where.Append(" AND c.StatusCode = @statusCode");
        if (!string.IsNullOrWhiteSpace(severityCode))
            where.Append(" AND r.SeverityCode = @severityCode");

        var p = new
        {
            tenantId,
            search       = $"%{searchTerm}%",
            statusCode,
            severityCode,
            offset       = (pageNumber - 1) * pageSize,
            pageSize,
        };

        var countSql = $"SELECT COUNT(*) FROM IAM.SodConflict c {Joins} {where}";
        var dataSql  = $@"
            SELECT {SelectColumns}
            FROM   IAM.SodConflict c
            {Joins}
            {where}
            ORDER BY c.DetectedDateUtc DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var total = await conn.QuerySingleAsync<int>(countSql, p);
        var items = (await conn.QueryAsync<SodConflictDto>(dataSql, p)).ToList();

        return new PagedResult<SodConflictDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize,
        };
    }

    // ── AssignReviewer ─────────────────────────────────────────────────────────
    public async Task AssignReviewerAsync(Guid id, AssignSodConflictReviewerRequest request, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var sql = @"
            UPDATE IAM.SodConflict
            SET    ReviewerUserId = @ReviewerUserId,
                   StatusCode     = CASE WHEN StatusCode = 'Open' THEN 'InReview' ELSE StatusCode END,
                   ModifiedDateUtc = GETUTCDATE()
            WHERE  SodConflictId = @id AND IsDeleted = 0";
        await conn.ExecuteAsync(sql, new { id, request.ReviewerUserId });
    }

    // ── Remediate ──────────────────────────────────────────────────────────────
    public async Task RemediateAsync(Guid id, RemediateSodConflictRequest request, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var sql = @"
            UPDATE IAM.SodConflict
            SET    StatusCode       = 'Remediated',
                   RemediationNote  = @RemediationNote,
                   ModifiedDateUtc  = GETUTCDATE()
            WHERE  SodConflictId = @id AND IsDeleted = 0";
        await conn.ExecuteAsync(sql, new { id, request.RemediationNote });
    }

    // ── Resolve ────────────────────────────────────────────────────────────────
    public async Task ResolveAsync(Guid id, ResolveSodConflictRequest request, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var sql = @"
            UPDATE IAM.SodConflict
            SET    StatusCode       = 'Resolved',
                   ResolvedByUserId = @ResolvedByUserId,
                   ResolutionNote   = @ResolutionNote,
                   ResolvedDateUtc  = GETUTCDATE(),
                   ModifiedDateUtc  = GETUTCDATE()
            WHERE  SodConflictId = @id AND IsDeleted = 0";
        await conn.ExecuteAsync(sql, new { id, request.ResolvedByUserId, request.ResolutionNote });
    }

    // ── CreateException ────────────────────────────────────────────────────────
    public async Task CreateExceptionAsync(Guid id, CreateSodExceptionRequest request, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var sql = @"
            UPDATE IAM.SodConflict
            SET    StatusCode      = 'Exception',
                   RemediationNote = @Justification,
                   ModifiedDateUtc = GETUTCDATE()
            WHERE  SodConflictId = @id AND IsDeleted = 0";
        await conn.ExecuteAsync(sql, new { id, request.Justification });
    }
}
