using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPayoutRepository : ICommissionPayoutRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionPayoutRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionPayoutDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PayoutId, TenantId, PayeeId, PayoutDate, TotalAmount, StatusCode, ProcessedDateUtc, Notes, CreatedDateUtc FROM Commission.CommissionPayout WHERE PayoutId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPayoutDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPayoutDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionPayout", "PayoutId, TenantId, PayeeId, PayoutDate, TotalAmount, StatusCode, ProcessedDateUtc, Notes, CreatedDateUtc", "Notes LIKE '%' + @SearchTerm + '%'", "PayoutDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionPayoutDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionPayoutDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
