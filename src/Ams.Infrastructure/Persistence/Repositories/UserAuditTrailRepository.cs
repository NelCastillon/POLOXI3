using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class UserAuditTrailRepository : IUserAuditTrailRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UserAuditTrailRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> LogAsync(LogUserAuditTrailRequest request, CancellationToken cancellationToken = default)
    {
        var auditTrailId = Guid.NewGuid();

        const string sql = """
            INSERT INTO IAM.UserAuditTrail
                (AuditTrailId, TenantId, UserId, ActionCode, ActionDescription, OldValue, NewValue,
                 ChangedByUserId, IpAddress, UserAgent, SessionId, StatusCode, ErrorDetails,
                 CreatedDateUtc, IsDeleted)
            VALUES
                (@AuditTrailId, @TenantId, @UserId, @ActionCode, @ActionDescription, @OldValue, @NewValue,
                 @ChangedByUserId, @IpAddress, @UserAgent, @SessionId, @StatusCode, @ErrorDetails,
                 SYSUTCDATETIME(), 0);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AuditTrailId = auditTrailId,
            request.TenantId,
            request.UserId,
            request.ActionCode,
            request.ActionDescription,
            request.OldValue,
            request.NewValue,
            request.ChangedByUserId,
            request.IpAddress,
            request.UserAgent,
            request.SessionId,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Success" : request.StatusCode,
            request.ErrorDetails
        }, cancellationToken: cancellationToken));

        return auditTrailId;
    }

    public async Task<PagedResult<UserAuditTrailDto>> SearchAsync(SearchUserAuditTrailRequest request, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        const string sql = """
            ;WITH Filtered AS
            (
                SELECT
                    a.AuditTrailId,
                    a.TenantId,
                    a.UserId,
                    u.UserName,
                    u.Email AS UserEmail,
                    u.FullName AS UserFullName,
                    a.ActionCode,
                    COALESCE(t.ActionName, a.ActionCode) AS ActionName,
                    COALESCE(t.CategoryCode, 'User') AS CategoryCode,
                    COALESCE(t.SeverityCode, CASE WHEN a.StatusCode = 'Failed' THEN 'High' ELSE 'Info' END) AS SeverityCode,
                    a.ActionDescription,
                    a.OldValue,
                    a.NewValue,
                    a.ChangedByUserId,
                    changed.UserName AS ChangedByUserName,
                    changed.FullName AS ChangedByFullName,
                    a.IpAddress,
                    a.UserAgent,
                    a.SessionId,
                    a.StatusCode,
                    a.ErrorDetails,
                    a.CreatedDateUtc,
                    a.ModifiedDateUtc,
                    a.ModifiedByUserId,
                    a.IsDeleted
                FROM IAM.UserAuditTrail a
                LEFT JOIN IAM.[User] u ON u.UserId = a.UserId
                LEFT JOIN IAM.[User] changed ON changed.UserId = a.ChangedByUserId
                LEFT JOIN IAM.UserAuditActionType t
                    ON t.TenantId = a.TenantId
                   AND t.ActionCode = a.ActionCode
                   AND t.IsDeleted = 0
                WHERE a.TenantId = @TenantId
                  AND a.IsDeleted = 0
                  AND (@UserId IS NULL OR a.UserId = @UserId)
                  AND (@ActionCode IS NULL OR @ActionCode = '' OR a.ActionCode = @ActionCode)
                  AND (@CategoryCode IS NULL OR @CategoryCode = '' OR COALESCE(t.CategoryCode, 'User') = @CategoryCode)
                  AND (@SeverityCode IS NULL OR @SeverityCode = '' OR COALESCE(t.SeverityCode, CASE WHEN a.StatusCode = 'Failed' THEN 'High' ELSE 'Info' END) = @SeverityCode)
                  AND (@StatusCode IS NULL OR @StatusCode = '' OR a.StatusCode = @StatusCode)
                  AND (@FromDateUtc IS NULL OR a.CreatedDateUtc >= @FromDateUtc)
                  AND (@ToDateUtc IS NULL OR a.CreatedDateUtc <= @ToDateUtc)
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR a.ActionCode LIKE '%' + @SearchTerm + '%'
                       OR a.ActionDescription LIKE '%' + @SearchTerm + '%'
                       OR a.IpAddress LIKE '%' + @SearchTerm + '%'
                       OR a.StatusCode LIKE '%' + @SearchTerm + '%'
                       OR u.UserName LIKE '%' + @SearchTerm + '%'
                       OR u.Email LIKE '%' + @SearchTerm + '%'
                       OR u.FullName LIKE '%' + @SearchTerm + '%'
                       OR changed.UserName LIKE '%' + @SearchTerm + '%'
                       OR changed.FullName LIKE '%' + @SearchTerm + '%')
            )
            SELECT COUNT(1) FROM Filtered;

            ;WITH Filtered AS
            (
                SELECT
                    a.AuditTrailId,
                    a.TenantId,
                    a.UserId,
                    u.UserName,
                    u.Email AS UserEmail,
                    u.FullName AS UserFullName,
                    a.ActionCode,
                    COALESCE(t.ActionName, a.ActionCode) AS ActionName,
                    COALESCE(t.CategoryCode, 'User') AS CategoryCode,
                    COALESCE(t.SeverityCode, CASE WHEN a.StatusCode = 'Failed' THEN 'High' ELSE 'Info' END) AS SeverityCode,
                    a.ActionDescription,
                    a.OldValue,
                    a.NewValue,
                    a.ChangedByUserId,
                    changed.UserName AS ChangedByUserName,
                    changed.FullName AS ChangedByFullName,
                    a.IpAddress,
                    a.UserAgent,
                    a.SessionId,
                    a.StatusCode,
                    a.ErrorDetails,
                    a.CreatedDateUtc,
                    a.ModifiedDateUtc,
                    a.ModifiedByUserId,
                    a.IsDeleted
                FROM IAM.UserAuditTrail a
                LEFT JOIN IAM.[User] u ON u.UserId = a.UserId
                LEFT JOIN IAM.[User] changed ON changed.UserId = a.ChangedByUserId
                LEFT JOIN IAM.UserAuditActionType t
                    ON t.TenantId = a.TenantId
                   AND t.ActionCode = a.ActionCode
                   AND t.IsDeleted = 0
                WHERE a.TenantId = @TenantId
                  AND a.IsDeleted = 0
                  AND (@UserId IS NULL OR a.UserId = @UserId)
                  AND (@ActionCode IS NULL OR @ActionCode = '' OR a.ActionCode = @ActionCode)
                  AND (@CategoryCode IS NULL OR @CategoryCode = '' OR COALESCE(t.CategoryCode, 'User') = @CategoryCode)
                  AND (@SeverityCode IS NULL OR @SeverityCode = '' OR COALESCE(t.SeverityCode, CASE WHEN a.StatusCode = 'Failed' THEN 'High' ELSE 'Info' END) = @SeverityCode)
                  AND (@StatusCode IS NULL OR @StatusCode = '' OR a.StatusCode = @StatusCode)
                  AND (@FromDateUtc IS NULL OR a.CreatedDateUtc >= @FromDateUtc)
                  AND (@ToDateUtc IS NULL OR a.CreatedDateUtc <= @ToDateUtc)
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR a.ActionCode LIKE '%' + @SearchTerm + '%'
                       OR a.ActionDescription LIKE '%' + @SearchTerm + '%'
                       OR a.IpAddress LIKE '%' + @SearchTerm + '%'
                       OR a.StatusCode LIKE '%' + @SearchTerm + '%'
                       OR u.UserName LIKE '%' + @SearchTerm + '%'
                       OR u.Email LIKE '%' + @SearchTerm + '%'
                       OR u.FullName LIKE '%' + @SearchTerm + '%'
                       OR changed.UserName LIKE '%' + @SearchTerm + '%'
                       OR changed.FullName LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Filtered
            ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.UserId,
            SearchTerm = request.SearchTerm?.Trim(),
            request.ActionCode,
            request.CategoryCode,
            request.SeverityCode,
            request.StatusCode,
            request.FromDateUtc,
            request.ToDateUtc,
            Offset = (pageNumber - 1) * pageSize,
            PageSize = pageSize
        }, cancellationToken: cancellationToken));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<UserAuditTrailDto>()).AsList();

        return new PagedResult<UserAuditTrailDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<UserAuditTrailDto?> GetByIdAsync(Guid tenantId, Guid auditTrailId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.AuditTrailId,
                a.TenantId,
                a.UserId,
                u.UserName,
                u.Email AS UserEmail,
                u.FullName AS UserFullName,
                a.ActionCode,
                COALESCE(t.ActionName, a.ActionCode) AS ActionName,
                COALESCE(t.CategoryCode, 'User') AS CategoryCode,
                COALESCE(t.SeverityCode, CASE WHEN a.StatusCode = 'Failed' THEN 'High' ELSE 'Info' END) AS SeverityCode,
                a.ActionDescription,
                a.OldValue,
                a.NewValue,
                a.ChangedByUserId,
                changed.UserName AS ChangedByUserName,
                changed.FullName AS ChangedByFullName,
                a.IpAddress,
                a.UserAgent,
                a.SessionId,
                a.StatusCode,
                a.ErrorDetails,
                a.CreatedDateUtc,
                a.ModifiedDateUtc,
                a.ModifiedByUserId,
                a.IsDeleted
            FROM IAM.UserAuditTrail a
            LEFT JOIN IAM.[User] u ON u.UserId = a.UserId
            LEFT JOIN IAM.[User] changed ON changed.UserId = a.ChangedByUserId
            LEFT JOIN IAM.UserAuditActionType t
                ON t.TenantId = a.TenantId
               AND t.ActionCode = a.ActionCode
               AND t.IsDeleted = 0
            WHERE a.TenantId = @TenantId
              AND a.AuditTrailId = @AuditTrailId
              AND a.IsDeleted = 0;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserAuditTrailDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, AuditTrailId = auditTrailId }, cancellationToken: cancellationToken));
    }

    public async Task<UserAuditTrailSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT(1) AS TotalEvents,
                COUNT(CASE WHEN a.StatusCode = 'Success' THEN 1 END) AS SuccessfulEvents,
                COUNT(CASE WHEN a.StatusCode = 'Failed' THEN 1 END) AS FailedEvents,
                COUNT(CASE WHEN COALESCE(t.CategoryCode, 'User') = 'Access' THEN 1 END) AS AccessChanges,
                COUNT(CASE WHEN COALESCE(t.CategoryCode, 'User') = 'Authentication' THEN 1 END) AS AuthenticationEvents,
                COUNT(CASE WHEN COALESCE(t.SeverityCode, CASE WHEN a.StatusCode = 'Failed' THEN 'High' ELSE 'Info' END) = 'High' THEN 1 END) AS HighSeverityEvents,
                COUNT(DISTINCT a.UserId) AS UniqueUsers,
                MAX(a.CreatedDateUtc) AS LastEventDateUtc
            FROM IAM.UserAuditTrail a
            LEFT JOIN IAM.UserAuditActionType t
                ON t.TenantId = a.TenantId
               AND t.ActionCode = a.ActionCode
               AND t.IsDeleted = 0
            WHERE a.TenantId = @TenantId
              AND a.IsDeleted = 0
              AND (@FromDateUtc IS NULL OR a.CreatedDateUtc >= @FromDateUtc)
              AND (@ToDateUtc IS NULL OR a.CreatedDateUtc <= @ToDateUtc);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<UserAuditTrailSummaryDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, FromDateUtc = fromDateUtc, ToDateUtc = toDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<UserAuditActionTypeDto>> GetActionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT UserAuditActionTypeId, TenantId, ActionCode, ActionName, CategoryCode, SeverityCode,
                   Description, SortOrder, IsActive
            FROM IAM.UserAuditActionType
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
            ORDER BY SortOrder, ActionName;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<UserAuditActionTypeDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return items.AsList();
    }
}
