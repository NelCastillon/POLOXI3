using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BankReconciliationRepository : IBankReconciliationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.BankReconciliation",
        "ReconciliationId, TenantId, BankAccountCode, StatementDate, StatementBalance, BookBalance, StatusCode, ReconciledDateUtc, ReconciledByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "BankAccountCode LIKE '%' + @SearchTerm + '%'",
        "StatementDate DESC");

    public BankReconciliationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<BankReconciliationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ReconciliationId, TenantId, BankAccountCode, StatementDate, StatementBalance, BookBalance, StatusCode, ReconciledDateUtc, ReconciledByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.BankReconciliation WHERE ReconciliationId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BankReconciliationDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BankReconciliationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BankReconciliationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BankReconciliationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
