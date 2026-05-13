using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BankReconciliationRepository : IBankReconciliationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BankReconciliationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<BankReconciliationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, 
    BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy,
    StatusCode, CreatedDateUtc 
FROM Finance.BankReconciliation 
WHERE BankReconciliationId = @Id AND IsDeleted = 0";
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BankReconciliationDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BankReconciliationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var selectColumns = "BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, CreatedDateUtc";
        var searchPredicate = "BankAccountNumber LIKE '%' + @SearchTerm + '%' OR BankName LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Finance.BankReconciliation", selectColumns, searchPredicate, "BankStatementDate DESC");
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BankReconciliationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BankReconciliationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var discrepancy = request.BankBalance - request.BookBalance;
        const string sql = @"
INSERT INTO Finance.BankReconciliation (BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @BankAccountNumber, @BankName, @BankStatementDate, @BankBalance, @BookBalance, 0, 0, @Discrepancy, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.BankAccountNumber, request.BankName, request.BankStatementDate, request.BankBalance, request.BookBalance, Discrepancy = discrepancy, request.StatusCode, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var discrepancy = request.BankBalance - request.BookBalance;
        const string sql = @"
UPDATE Finance.BankReconciliation
SET BankAccountNumber = @BankAccountNumber,
    BankName = @BankName,
    BankStatementDate = @BankStatementDate,
    BankBalance = @BankBalance,
    BookBalance = @BookBalance,
    Discrepancy = @Discrepancy,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE BankReconciliationId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.BankAccountNumber, request.BankName, request.BankStatementDate, request.BankBalance, request.BookBalance, Discrepancy = discrepancy, request.StatusCode, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
