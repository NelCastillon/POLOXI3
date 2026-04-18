using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class OpportunityRepository : IOpportunityRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public OpportunityRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO CRM.Opportunity
(
    OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
    EstimatedAmount, OwnerUserId, CloseDate, LeadId, WinProbability,
    ForecastCategoryCode, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @OpportunityId, @TenantId, @OpportunityNumber, @AccountId, @OpportunityName,
    @EstimatedAmount, @OwnerUserId, @CloseDate, @LeadId, @WinProbability,
    @ForecastCategoryCode, 1, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            OpportunityId = id,
            request.TenantId,
            request.OpportunityNumber,
            request.AccountId,
            request.OpportunityName,
            request.EstimatedAmount,
            request.OwnerUserId,
            request.CloseDate,
            request.LeadId,
            request.WinProbability,
            request.ForecastCategoryCode,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT o.OpportunityId, o.TenantId, o.OpportunityNumber, o.AccountId,
       a.AccountName, o.OpportunityName, o.EstimatedAmount,
       o.StatusCodeId AS StatusCode, o.OwnerUserId,
       o.CloseDate, o.WinProbability, o.ForecastCategoryCode, o.LeadId
FROM CRM.Opportunity o
LEFT JOIN Client.Account a ON a.AccountId = o.AccountId
WHERE o.OpportunityId = @Id AND o.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<OpportunityDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT o.OpportunityId, o.TenantId, o.OpportunityNumber, o.AccountId,
           a.AccountName, o.OpportunityName, o.EstimatedAmount,
           o.StatusCodeId AS StatusCode, o.OwnerUserId,
           o.CloseDate, o.WinProbability, o.ForecastCategoryCode, o.LeadId,
           o.CreatedDateUtc
    FROM CRM.Opportunity o
    LEFT JOIN Client.Account a ON a.AccountId = o.AccountId
    WHERE o.TenantId = @TenantId AND o.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR o.OpportunityName LIKE '%' + @SearchTerm + '%'
           OR o.OpportunityNumber LIKE '%' + @SearchTerm + '%'
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
      )
)
SELECT * FROM Paged ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM CRM.Opportunity o
WHERE o.TenantId = @TenantId AND o.IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR o.OpportunityName LIKE '%' + @SearchTerm + '%'
       OR o.OpportunityNumber LIKE '%' + @SearchTerm + '%'
  );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<OpportunityDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<OpportunityDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
