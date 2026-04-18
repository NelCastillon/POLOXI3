using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionTransactionRepository : ICommissionTransactionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionTransactionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionTransactionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc FROM Commission.CommissionTransaction WHERE TransactionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionTransactionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionTransactionDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionTransaction", "TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc", "SourceEntityName LIKE '%' + @SearchTerm + '%'", "TransactionDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionTransactionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionTransactionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
