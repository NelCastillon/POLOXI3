using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RateCardLineRepository : IRateCardLineRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RateCardLineRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RateCardLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT RateCardLineId, TenantId, RateCardId, RoleCode, ServiceCode, Description, HourlyRate, DailyRate, EffectiveStartDate, EffectiveEndDate, IsActive, CreatedDateUtc FROM Billing.RateCardLine WHERE RateCardLineId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RateCardLineDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RateCardLineDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.RateCardLine", "RateCardLineId, TenantId, RateCardId, RoleCode, ServiceCode, Description, HourlyRate, DailyRate, EffectiveStartDate, EffectiveEndDate, IsActive, CreatedDateUtc", "RoleCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%'", "HourlyRate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RateCardLineDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RateCardLineDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
