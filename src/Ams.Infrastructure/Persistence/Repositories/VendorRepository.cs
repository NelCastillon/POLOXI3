using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class VendorRepository : IVendorRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.Vendor",
        "VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted",
        "VendorCode LIKE '%' + @SearchTerm + '%' OR VendorName LIKE '%' + @SearchTerm + '%'",
        "VendorName ASC");

    public VendorRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<VendorDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted FROM Finance.Vendor WHERE VendorId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<VendorDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<VendorDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<VendorDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<VendorDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
