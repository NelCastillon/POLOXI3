using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ApPaymentRepository : IApPaymentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ApPaymentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ApPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    ApPaymentId, TenantId, VendorId, ApInvoiceId, PaymentDate, Amount, 
    PaymentMethodCode, ReferenceNumber, Notes, StatusCode, CreatedDateUtc 
FROM Finance.ApPayment 
WHERE ApPaymentId = @Id AND IsDeleted = 0";
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ApPaymentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ApPaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var selectColumns = "ApPaymentId, TenantId, VendorId, ApInvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, Notes, StatusCode, CreatedDateUtc";
        var searchPredicate = "ReferenceNumber LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.ApPayment", selectColumns, searchPredicate, "PaymentDate DESC");
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ApPaymentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ApPaymentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
