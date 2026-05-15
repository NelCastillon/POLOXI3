using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Engagements;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class EngagementRepository : IEngagementRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public EngagementRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<EngagementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EngagementId, TenantId, EngagementNumber, AccountId, AgreementId, EngagementName, EngagementTypeCode, OwnerUserId, StartDate, EndDate, StatusCode, CreatedDateUtc FROM OPS.Engagement WHERE EngagementId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<EngagementDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<EngagementDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("OPS.Engagement", "EngagementId, TenantId, EngagementNumber, AccountId, AgreementId, EngagementName, EngagementTypeCode, OwnerUserId, StartDate, EndDate, StatusCode, CreatedDateUtc", "EngagementName LIKE '%' + @SearchTerm + '%' OR EngagementNumber LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<EngagementDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<EngagementDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<EngagementTaskDto>> SearchTasksAsync(Guid tenantId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT TaskId, TenantId, EngagementId, MilestoneId, TaskTitle, AssignedToUserId, DueDate, StatusCode, Priority, CreatedDateUtc FROM OPS.EngagementTask WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TaskTitle LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.EngagementTask WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR TaskTitle LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EngagementId = engagementId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<EngagementTaskDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<EngagementTaskDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateEngagementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.Engagement (EngagementId, TenantId, EngagementNumber, AccountId, AgreementId, EngagementName, EngagementTypeCode, OwnerUserId, StartDate, EndDate, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@EngagementId, @TenantId, @EngagementNumber, @AccountId, @AgreementId, @EngagementName, @EngagementTypeCode, @OwnerUserId, @StartDate, @EndDate, 'Active', SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EngagementId = id, request.TenantId, request.EngagementNumber, request.AccountId, request.AgreementId, request.EngagementName, request.EngagementTypeCode, request.OwnerUserId, request.StartDate, request.EndDate, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateEngagementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.Engagement
SET EngagementNumber = @EngagementNumber,
    AccountId = @AccountId,
    AgreementId = @AgreementId,
    EngagementName = @EngagementName,
    EngagementTypeCode = @EngagementTypeCode,
    OwnerUserId = @OwnerUserId,
    StartDate = @StartDate,
    EndDate = @EndDate,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EngagementId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EngagementNumber, request.AccountId, request.AgreementId, request.EngagementName, request.EngagementTypeCode, request.OwnerUserId, request.StartDate, request.EndDate, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.Engagement
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EngagementId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
