using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
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

    public async Task<Guid> CreateAsync(CreateVendorRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.Vendor (VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @VendorCode, @VendorName, @ContactName, @Email, @Phone, @PaymentTermsCode, @CurrencyCode, @TaxId, @VendorTypeCode, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.VendorCode, request.VendorName, request.ContactName, request.Email, request.Phone, request.PaymentTermsCode, request.CurrencyCode, request.TaxId, request.VendorTypeCode, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateVendorRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Finance.Vendor
SET VendorCode = @VendorCode,
    VendorName = @VendorName,
    ContactName = @ContactName,
    Email = @Email,
    Phone = @Phone,
    PaymentTermsCode = @PaymentTermsCode,
    CurrencyCode = @CurrencyCode,
    TaxId = @TaxId,
    VendorTypeCode = @VendorTypeCode,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE VendorId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.VendorCode, request.VendorName, request.ContactName, request.Email, request.Phone, request.PaymentTermsCode, request.CurrencyCode, request.TaxId, request.VendorTypeCode, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
