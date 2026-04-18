using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionPayeeRepository : ICommissionPayeeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommissionPayeeRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CommissionPayeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc FROM Commission.CommissionPayee WHERE PayeeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommissionPayeeDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CommissionPayeeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Commission.CommissionPayee", "PayeeId, TenantId, UserId, CommissionPlanId, PayeeTypeCode, SplitPercentage, EffectiveDate, StatusCode, CreatedDateUtc", "PayeeTypeCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CommissionPayeeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CommissionPayeeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
