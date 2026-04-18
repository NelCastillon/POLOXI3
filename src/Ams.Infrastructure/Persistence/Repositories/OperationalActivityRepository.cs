using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class OperationalActivityRepository : IOperationalActivityRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public OperationalActivityRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<OperationalActivityLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ActivityId, TenantId, AccountId, EngagementId, AgreementId, ActivityDate, ActivityTypeCode, Subject, Notes, PerformedByUserId, CreatedDateUtc FROM OPS.OperationalActivityLog WHERE ActivityId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<OperationalActivityLogDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<OperationalActivityLogDto>> SearchAsync(Guid tenantId, Guid? accountId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT ActivityId, TenantId, AccountId, EngagementId, AgreementId, ActivityDate, ActivityTypeCode, Subject, Notes, PerformedByUserId, CreatedDateUtc FROM OPS.OperationalActivityLog WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AccountId IS NULL OR AccountId = @AccountId) AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY ActivityDate DESC, CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.OperationalActivityLog WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AccountId IS NULL OR AccountId = @AccountId) AND (@EngagementId IS NULL OR EngagementId = @EngagementId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AccountId = accountId, EngagementId = engagementId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<OperationalActivityLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<OperationalActivityLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateOperationalActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.OperationalActivityLog (ActivityId, TenantId, AccountId, EngagementId, AgreementId, ActivityDate, ActivityTypeCode, Subject, Notes, PerformedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@ActivityId, @TenantId, @AccountId, @EngagementId, @AgreementId, @ActivityDate, @ActivityTypeCode, @Subject, @Notes, @PerformedByUserId, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ActivityId = id, request.TenantId, request.AccountId, request.EngagementId, request.AgreementId, request.ActivityDate, request.ActivityTypeCode, request.Subject, request.Notes, request.PerformedByUserId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }
}
