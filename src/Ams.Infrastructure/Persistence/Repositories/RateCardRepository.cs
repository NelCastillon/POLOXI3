using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RateCardRepository : IRateCardRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RateCardRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RateCardDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT RateCardId, TenantId, RateCardCode, RateCardName, EffectiveStartDate, EffectiveEndDate, StatusCode, Description, CreatedDateUtc FROM Billing.RateCard WHERE RateCardId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RateCardDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RateCardDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.RateCard", "RateCardId, TenantId, RateCardCode, RateCardName, EffectiveStartDate, EffectiveEndDate, StatusCode, Description, CreatedDateUtc", "RateCardCode LIKE '%' + @SearchTerm + '%' OR RateCardName LIKE '%' + @SearchTerm + '%'", "RateCardName ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RateCardDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RateCardDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
