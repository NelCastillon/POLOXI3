using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RetainerDrawdownRepository : IRetainerDrawdownRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RetainerDrawdownRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RetainerDrawdownDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DrawdownId, TenantId, RetainerAccountId, InvoiceId, DrawdownDate, Amount, Description, CreatedDateUtc FROM Billing.RetainerDrawdown WHERE DrawdownId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RetainerDrawdownDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RetainerDrawdownDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.RetainerDrawdown", "DrawdownId, TenantId, RetainerAccountId, InvoiceId, DrawdownDate, Amount, Description, CreatedDateUtc", "Description LIKE '%' + @SearchTerm + '%'", "DrawdownDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RetainerDrawdownDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RetainerDrawdownDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
