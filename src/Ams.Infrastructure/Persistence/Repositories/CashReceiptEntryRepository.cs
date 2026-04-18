using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CashReceiptEntryRepository : ICashReceiptEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.CashReceiptEntry",
        "CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted",
        "ReferenceNumber LIKE '%' + @SearchTerm + '%'",
        "ReceiptDate DESC");

    public CashReceiptEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CashReceiptEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted FROM Finance.CashReceiptEntry WHERE CashReceiptEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CashReceiptEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CashReceiptEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CashReceiptEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CashReceiptEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
