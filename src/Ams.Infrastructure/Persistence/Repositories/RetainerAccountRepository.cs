using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RetainerAccountRepository : IRetainerAccountRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RetainerAccountRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RetainerAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT RetainerAccountId, TenantId, AccountId, AgreementId, RetainerName, TotalAmount, UsedAmount, RemainingAmount, PeriodStart, PeriodEnd, StatusCode, CreatedDateUtc FROM Billing.RetainerAccount WHERE RetainerAccountId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RetainerAccountDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RetainerAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.RetainerAccount", "RetainerAccountId, TenantId, AccountId, AgreementId, RetainerName, TotalAmount, UsedAmount, RemainingAmount, PeriodStart, PeriodEnd, StatusCode, CreatedDateUtc", "RetainerName LIKE '%' + @SearchTerm + '%'", "PeriodStart DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RetainerAccountDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RetainerAccountDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
