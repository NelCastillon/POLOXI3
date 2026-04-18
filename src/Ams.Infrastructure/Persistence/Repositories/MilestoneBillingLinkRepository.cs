using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MilestoneBillingLinkRepository : IMilestoneBillingLinkRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public MilestoneBillingLinkRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<MilestoneBillingLinkDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT LinkId, TenantId, MilestoneId, InvoiceId, BillingAmount, TriggeredDateUtc, StatusCode, Notes, CreatedDateUtc FROM Billing.MilestoneBillingLink WHERE LinkId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<MilestoneBillingLinkDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<MilestoneBillingLinkDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.MilestoneBillingLink", "LinkId, TenantId, MilestoneId, InvoiceId, BillingAmount, TriggeredDateUtc, StatusCode, Notes, CreatedDateUtc", "Notes LIKE '%' + @SearchTerm + '%' OR StatusCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<MilestoneBillingLinkDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<MilestoneBillingLinkDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
