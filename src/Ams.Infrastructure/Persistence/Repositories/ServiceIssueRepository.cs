using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ServiceIssueRepository : IServiceIssueRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ServiceIssueRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ServiceIssueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT IssueId, TenantId, EngagementId, AccountId, IssueNumber, Title, Description, SeverityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedDateUtc FROM OPS.IssueTracker WHERE IssueId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ServiceIssueDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ServiceIssueDto>> SearchAsync(Guid tenantId, Guid? engagementId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT IssueId, TenantId, EngagementId, AccountId, IssueNumber, Title, Description, SeverityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedDateUtc FROM OPS.IssueTracker WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@AccountId IS NULL OR AccountId = @AccountId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Title LIKE '%' + @SearchTerm + '%' OR IssueNumber LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.IssueTracker WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@AccountId IS NULL OR AccountId = @AccountId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Title LIKE '%' + @SearchTerm + '%' OR IssueNumber LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EngagementId = engagementId, AccountId = accountId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ServiceIssueDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ServiceIssueDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateServiceIssueRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.IssueTracker (IssueId, TenantId, EngagementId, AccountId, IssueNumber, Title, Description, SeverityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@IssueId, @TenantId, @EngagementId, @AccountId, @IssueNumber, @Title, @Description, @SeverityCode, @AssignedToUserId, 'Open', SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { IssueId = id, request.TenantId, request.EngagementId, request.AccountId, request.IssueNumber, request.Title, request.Description, request.SeverityCode, request.AssignedToUserId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateServiceIssueRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.IssueTracker
SET EngagementId = @EngagementId,
    AccountId = @AccountId,
    IssueNumber = @IssueNumber,
    Title = @Title,
    Description = @Description,
    SeverityCode = @SeverityCode,
    AssignedToUserId = @AssignedToUserId,
    StatusCode = @StatusCode,
    ResolvedDate = @ResolvedDate,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE IssueId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EngagementId, request.AccountId, request.IssueNumber, request.Title, request.Description, request.SeverityCode, request.AssignedToUserId, request.StatusCode, request.ResolvedDate, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.IssueTracker
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE IssueId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
