using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PaymentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PaymentId, TenantId, AccountId, InvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, StatusCode, Notes, CreatedDateUtc FROM Billing.Payment WHERE PaymentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PaymentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.Payment", "PaymentId, TenantId, AccountId, InvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, StatusCode, Notes, CreatedDateUtc", "ReferenceNumber LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%'", "PaymentDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PaymentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PaymentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
