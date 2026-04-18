using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SecurityAuditRepository : ISecurityAuditRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SecurityAuditRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<PagedResult<FieldChangeLogDto>> SearchFieldChangesAsync(Guid tenantId, string? entityName, Guid? entityId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT FieldChangeLogId, TenantId, EntityName, EntityId, FieldName,
                       OldValue, NewValue, ChangedByUserId, ChangedDateUtc, ChangeSource, IpAddress
                FROM Audit.FieldChangeLog
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@EntityName IS NULL OR EntityName = @EntityName)
                  AND (@EntityId IS NULL OR EntityId = @EntityId)
            )
            SELECT * FROM Cte ORDER BY ChangedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.FieldChangeLog
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@EntityName IS NULL OR EntityName = @EntityName)
              AND (@EntityId IS NULL OR EntityId = @EntityId);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityName = entityName, EntityId = entityId, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<FieldChangeLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<FieldChangeLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<SecurityEventLogDto>> SearchSecurityEventsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT SecurityEventId, TenantId, UserId, EventTypeCode, EventDescription,
                       IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc
                FROM Audit.SecurityEventLog
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR EventTypeCode    LIKE '%' + @SearchTerm + '%'
                                          OR EventDescription LIKE '%' + @SearchTerm + '%'
                                          OR IpAddress        LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.SecurityEventLog
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR EventTypeCode    LIKE '%' + @SearchTerm + '%'
                                      OR EventDescription LIKE '%' + @SearchTerm + '%'
                                      OR IpAddress        LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SecurityEventLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SecurityEventLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
