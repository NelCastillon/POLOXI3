using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class InvoiceLineRepository : IInvoiceLineRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public InvoiceLineRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<InvoiceLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT InvoiceLineId, TenantId, InvoiceId, LineOrder, ItemCode, Description, Quantity, UnitPrice, DiscountPercent, TaxPercent, LineTotal, SourceEntityName, SourceEntityId, CreatedDateUtc FROM Finance.InvoiceLine WHERE InvoiceLineId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<InvoiceLineDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<InvoiceLineDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Finance.InvoiceLine", "InvoiceLineId, TenantId, InvoiceId, LineOrder, ItemCode, Description, Quantity, UnitPrice, DiscountPercent, TaxPercent, LineTotal, SourceEntityName, SourceEntityId, CreatedDateUtc", "Description LIKE '%' + @SearchTerm + '%' OR ItemCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<InvoiceLineDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<InvoiceLineDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
