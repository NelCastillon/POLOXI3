namespace Ams.Infrastructure.Persistence.Repositories;

internal static class RepositorySql
{
    public static string BuildPagedSearchSql(string table, string selectColumns, string searchPredicate, string orderBy, bool hasSoftDelete = true)
    {
        var softDeletePredicate = hasSoftDelete ? "AND IsDeleted = 0" : string.Empty;

        return $@"
;WITH Cte AS
(
    SELECT {selectColumns}
    FROM {table}
    WHERE TenantId = @TenantId
      {softDeletePredicate}
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR {searchPredicate})
)
SELECT * FROM Cte
ORDER BY {orderBy}
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM {table}
WHERE TenantId = @TenantId
  {softDeletePredicate}
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR {searchPredicate});";
    }
}
