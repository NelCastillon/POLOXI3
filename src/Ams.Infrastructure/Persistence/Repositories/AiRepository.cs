using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AiRepository : IAiRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AiRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string InsightColumns = "InsightId, TenantId, Category, Title, Summary, ActionableRecommendation, Severity, GeneratedDateUtc";
    private const string ActionColumns  = "ActionId, TenantId, ActionType, Description, Priority, RelatedEntityId, RelatedEntityName, SuggestedByUtc";

    public async Task<PagedResult<AiInsightDto>> GetInsightsAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT InsightId, TenantId, Category, Title, Summary, ActionableRecommendation, Severity, GeneratedDateUtc
    FROM Ai.Insight
    WHERE TenantId = @TenantId AND IsDeleted = 0
)
SELECT * FROM Cte
ORDER BY GeneratedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Ai.Insight
WHERE TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AiInsightDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AiInsightDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<AiAccountSummaryDto?> GetAccountSummaryAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT AccountId, AccountName, Summary, HealthIndicator, GeneratedDateUtc
FROM Ai.AccountSummary
WHERE AccountId = @AccountId AND IsDeleted = 0;

SELECT Risk
FROM Ai.AccountSummaryRisk
WHERE AccountId = @AccountId;

SELECT Opportunity
FROM Ai.AccountSummaryOpportunity
WHERE AccountId = @AccountId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { AccountId = accountId }, cancellationToken: cancellationToken));
        var dto = await multi.ReadSingleOrDefaultAsync<AiAccountSummaryDto>();
        if (dto is null) return null;
        dto.KeyRisks = (await multi.ReadAsync<string>()).ToArray();
        dto.Opportunities = (await multi.ReadAsync<string>()).ToArray();
        return dto;
    }

    public async Task<PagedResult<AiNextActionDto>> GetNextActionsAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT ActionId, TenantId, ActionType, Description, Priority, RelatedEntityId, RelatedEntityName, SuggestedByUtc
    FROM Ai.NextAction
    WHERE TenantId = @TenantId AND IsDeleted = 0
)
SELECT * FROM Cte
ORDER BY SuggestedByUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Ai.NextAction
WHERE TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AiNextActionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AiNextActionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
