using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPlanVersionRepository : ICommissionPlanVersionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommissionPlanVersionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CommissionPlanVersionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT PlanVersionId, TenantId, CommissionPlanId, VersionNumber, PlanName, BaseRatePct, EffectiveStartDate, EffectiveEndDate, StatusCode, CreatedDateUtc FROM Commission.CommissionPlanVersion WHERE PlanVersionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPlanVersionDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPlanVersionDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Commission.CommissionPlanVersion",
            "PlanVersionId, TenantId, CommissionPlanId, VersionNumber, PlanName, BaseRatePct, EffectiveStartDate, EffectiveEndDate, StatusCode, CreatedDateUtc",
            "PlanName LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<CommissionPlanVersionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<CommissionPlanVersionDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
