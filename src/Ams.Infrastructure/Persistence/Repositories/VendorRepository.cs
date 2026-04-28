using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class VendorRepository : IVendorRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public VendorRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<VendorDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, 
    PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc 
FROM Finance.Vendor 
WHERE VendorId = @Id AND IsDeleted = 0";
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<VendorDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<VendorDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var selectColumns = "VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc";
        var searchPredicate = "VendorCode LIKE '%' + @SearchTerm + '%' OR VendorName LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.Vendor", selectColumns, searchPredicate, "VendorName ASC");
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<VendorDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<VendorDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
