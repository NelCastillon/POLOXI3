using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ApInvoiceRepository : IApInvoiceRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.ApInvoice",
        "ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, TotalAmount, PaidAmount, BalanceAmount, StatusCode, GLAccountId, AgreementId, Notes, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted",
        "InvoiceNumber LIKE '%' + @SearchTerm + '%'",
        "InvoiceDate DESC");

    public ApInvoiceRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ApInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, TotalAmount, PaidAmount, BalanceAmount, StatusCode, GLAccountId, AgreementId, Notes, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted FROM Finance.ApInvoice WHERE ApInvoiceId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ApInvoiceDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ApInvoiceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ApInvoiceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ApInvoiceDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
