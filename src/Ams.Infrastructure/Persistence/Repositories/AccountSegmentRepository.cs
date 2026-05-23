using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountSegments;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountSegmentRepository : IAccountSegmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountSegmentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Guid> CreateAsync(CreateAccountSegmentRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SegmentCode))
        {
            throw new InvalidOperationException("Segment code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SegmentName))
        {
            throw new InvalidOperationException("Segment name is required.");
        }

        const string sql = @"
INSERT INTO Client.AccountSegment
(
    SegmentId, TenantId, SegmentCode, SegmentName, Description,
    IsActive, CreatedDateUtc, IsDeleted
)
VALUES
(
    @SegmentId, @TenantId, @SegmentCode, @SegmentName, @Description,
    @IsActive, SYSUTCDATETIME(), 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SegmentId = id,
            request.TenantId,
            SegmentCode = request.SegmentCode.Trim(),
            SegmentName = request.SegmentName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.IsActive
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task UpdateAsync(UpdateAccountSegmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SegmentId == Guid.Empty)
        {
            throw new InvalidOperationException("Segment id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SegmentCode))
        {
            throw new InvalidOperationException("Segment code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SegmentName))
        {
            throw new InvalidOperationException("Segment name is required.");
        }

        const string sql = @"
UPDATE Client.AccountSegment
SET TenantId = @TenantId,
    SegmentCode = @SegmentCode,
    SegmentName = @SegmentName,
    Description = @Description,
    IsActive = @IsActive
WHERE SegmentId = @SegmentId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.SegmentId,
            request.TenantId,
            SegmentCode = request.SegmentCode.Trim(),
            SegmentName = request.SegmentName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.IsActive
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.AccountSegment
SET IsDeleted = 1,
    IsActive = 0
WHERE SegmentId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

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
