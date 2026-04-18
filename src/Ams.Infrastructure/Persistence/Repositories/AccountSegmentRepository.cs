using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountSegmentRepository : IAccountSegmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountSegmentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AccountSegmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT SegmentId, TenantId, SegmentCode, SegmentName, Description, IsActive, CreatedDateUtc
FROM Client.AccountSegment
WHERE SegmentId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountSegmentDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountSegmentDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT SegmentId, TenantId, SegmentCode, SegmentName, Description, IsActive, CreatedDateUtc
    FROM Client.AccountSegment
    WHERE IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR SegmentName LIKE '%' + @SearchTerm + '%'
           OR SegmentCode LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY SegmentName ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.AccountSegment
WHERE IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR SegmentName LIKE '%' + @SearchTerm + '%'
       OR SegmentCode LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AccountSegmentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AccountSegmentDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
