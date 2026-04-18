using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BadDebtEntryRepository : IBadDebtEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private static readonly string _searchSql = RepositorySql.BuildPagedSearchSql(
        "Finance.BadDebtEntry",
        "BadDebtEntryId, TenantId, AccountId, InvoiceId, WriteOffDate, Amount, Reason, GLAccountId, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted",
        "Reason LIKE '%' + @SearchTerm + '%'",
        "WriteOffDate DESC");

    public BadDebtEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<BadDebtEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT BadDebtEntryId, TenantId, AccountId, InvoiceId, WriteOffDate, Amount, Reason, GLAccountId, ApprovedByUserId, ApprovedDateUtc, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted FROM Finance.BadDebtEntry WHERE BadDebtEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BadDebtEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BadDebtEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(_searchSql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BadDebtEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BadDebtEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
