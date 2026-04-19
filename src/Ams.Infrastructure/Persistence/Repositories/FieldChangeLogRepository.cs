using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class FieldChangeLogRepository : IFieldChangeLogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public FieldChangeLogRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<FieldChangeLogDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT FieldChangeLogId, TenantId, EntityName, EntityId, FieldName, OldValue, NewValue,
           ChangedByUserId, ChangedDateUtc, ChangeSource, IpAddress
    FROM Audit.FieldChangeLog
    WHERE IsDeleted = 0
      AND TenantId = @TenantId
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR EntityName  LIKE '%' + @SearchTerm + '%'
           OR FieldName   LIKE '%' + @SearchTerm + '%'
           OR ChangeSource LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT FieldChangeLogId, TenantId, EntityName, EntityId, FieldName, OldValue, NewValue,
           ChangedByUserId, ChangedDateUtc, ChangeSource, IpAddress
    FROM Audit.FieldChangeLog
    WHERE IsDeleted = 0
      AND TenantId = @TenantId
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR EntityName  LIKE '%' + @SearchTerm + '%'
           OR FieldName   LIKE '%' + @SearchTerm + '%'
           OR ChangeSource LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY ChangedDateUtc DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<FieldChangeLogDto>()).ToList();
        return new PagedResult<FieldChangeLogDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
