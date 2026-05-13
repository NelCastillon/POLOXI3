using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
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
        if (!await TableExistsAsync(cancellationToken)) return null;

        const string sql = "SELECT CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted FROM Finance.CashReceiptEntry WHERE CashReceiptEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CashReceiptEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CashReceiptEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            return new PagedResult<CashReceiptEntryDto> { Items = [], TotalCount = 0, PageNumber = pageNumber, PageSize = pageSize };

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CashReceiptEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CashReceiptEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCashReceiptEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.CashReceiptEntry does not exist in the current database schema. Cash Receipts are unavailable until the database schema includes this table.");

        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.CashReceiptEntry (CashReceiptEntryId, TenantId, AccountId, InvoiceId, ReceiptDate, Amount, PaymentMethodCode, ReferenceNumber, GLAccountId, BankAccountCode, Notes, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @InvoiceId, @ReceiptDate, @Amount, @PaymentMethodCode, @ReferenceNumber, @GLAccountId, @BankAccountCode, @Notes, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.InvoiceId, request.ReceiptDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.GLAccountId, request.BankAccountCode, request.Notes, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCashReceiptEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(cancellationToken))
            throw new InvalidOperationException("Finance.CashReceiptEntry does not exist in the current database schema. Cash Receipts are unavailable until the database schema includes this table.");

        const string sql = @"
UPDATE Finance.CashReceiptEntry
SET AccountId = @AccountId,
    InvoiceId = @InvoiceId,
    ReceiptDate = @ReceiptDate,
    Amount = @Amount,
    PaymentMethodCode = @PaymentMethodCode,
    ReferenceNumber = @ReferenceNumber,
    GLAccountId = @GLAccountId,
    BankAccountCode = @BankAccountCode,
    Notes = @Notes,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CashReceiptEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.InvoiceId, request.ReceiptDate, request.Amount, request.PaymentMethodCode, request.ReferenceNumber, request.GLAccountId, request.BankAccountCode, request.Notes, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task<bool> TableExistsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'Finance.CashReceiptEntry', N'U') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
