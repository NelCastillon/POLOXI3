using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Engagements;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class EngagementMilestoneRepository : IEngagementMilestoneRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public EngagementMilestoneRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<EngagementMilestoneDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT MilestoneId, TenantId, EngagementId, MilestoneName, DueDate, CompletedDate, StatusCode, CreatedDateUtc FROM OPS.EngagementMilestone WHERE MilestoneId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<EngagementMilestoneDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<EngagementMilestoneDto>> SearchAsync(Guid tenantId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT MilestoneId, TenantId, EngagementId, MilestoneName, DueDate, CompletedDate, StatusCode, CreatedDateUtc FROM OPS.EngagementMilestone WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR MilestoneName LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.EngagementMilestone WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR MilestoneName LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EngagementId = engagementId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<EngagementMilestoneDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<EngagementMilestoneDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateEngagementMilestoneRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.EngagementMilestone (MilestoneId, TenantId, EngagementId, MilestoneName, DueDate, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@MilestoneId, @TenantId, @EngagementId, @MilestoneName, @DueDate, 'Pending', SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { MilestoneId = id, request.TenantId, request.EngagementId, request.MilestoneName, request.DueDate, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }
}
