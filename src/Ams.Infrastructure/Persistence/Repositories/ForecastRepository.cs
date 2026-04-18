using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Forecast;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ForecastRepository : IForecastRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ForecastRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateForecastEntryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO CRM.ForecastEntry
(
    ForecastEntryId, TenantId, OpportunityId, OwnerUserId,
    ForecastPeriod, ForecastAmount, PipelineAmount, CategoryCode,
    CloseDate, WinProbability, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @ForecastEntryId, @TenantId, @OpportunityId, @OwnerUserId,
    @ForecastPeriod, @ForecastAmount, @PipelineAmount, @CategoryCode,
    @CloseDate, @WinProbability, @Notes, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ForecastEntryId = id,
            request.TenantId,
            request.OpportunityId,
            request.OwnerUserId,
            request.ForecastPeriod,
            request.ForecastAmount,
            request.PipelineAmount,
            request.CategoryCode,
            request.CloseDate,
            request.WinProbability,
            request.Notes,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<ForecastEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT f.ForecastEntryId, f.TenantId, f.OpportunityId, o.OpportunityName,
       f.OwnerUserId, f.ForecastPeriod, f.ForecastAmount, f.PipelineAmount,
       f.CategoryCode, f.CloseDate, f.WinProbability, f.Notes, f.CreatedDateUtc
FROM CRM.ForecastEntry f
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = f.OpportunityId
WHERE f.ForecastEntryId = @Id AND f.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ForecastEntryDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ForecastEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT f.ForecastEntryId, f.TenantId, f.OpportunityId, o.OpportunityName,
           f.OwnerUserId, f.ForecastPeriod, f.ForecastAmount, f.PipelineAmount,
           f.CategoryCode, f.CloseDate, f.WinProbability, f.Notes, f.CreatedDateUtc
    FROM CRM.ForecastEntry f
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = f.OpportunityId
    WHERE f.TenantId = @TenantId AND f.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR f.ForecastPeriod LIKE '%' + @SearchTerm + '%'
           OR f.CategoryCode LIKE '%' + @SearchTerm + '%'
           OR o.OpportunityName LIKE '%' + @SearchTerm + '%'
      )
)
SELECT * FROM Paged ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM CRM.ForecastEntry WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ForecastPeriod LIKE '%' + @SearchTerm + '%' OR CategoryCode LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<ForecastEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<ForecastEntryDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
