using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ApInvoiceLineRepository : IApInvoiceLineRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.ApInvoiceLine",
        "ApInvoiceLineId, TenantId, ApInvoiceId, LineOrder, Description, Quantity, UnitPrice, LineTotal, GLAccountId, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "Description LIKE '%' + @SearchTerm + '%'",
        "LineOrder ASC");

    public ApInvoiceLineRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ApInvoiceLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ApInvoiceLineId, TenantId, ApInvoiceId, LineOrder, Description, Quantity, UnitPrice, LineTotal, GLAccountId, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.ApInvoiceLine WHERE ApInvoiceLineId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ApInvoiceLineDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ApInvoiceLineDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ApInvoiceLineDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ApInvoiceLineDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
