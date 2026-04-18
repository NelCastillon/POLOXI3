using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DelinquencyFlagRepository : IDelinquencyFlagRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public DelinquencyFlagRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<DelinquencyFlagDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT DelinquencyFlagId, TenantId, AccountId, InvoiceId, FlagDate, DaysOverdue, OverdueAmount, SeverityCode, StatusCode, ResolvedDate, Notes, AssignedToUserId, CreatedDateUtc FROM Billing.DelinquencyFlag WHERE DelinquencyFlagId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DelinquencyFlagDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<DelinquencyFlagDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.DelinquencyFlag", "DelinquencyFlagId, TenantId, AccountId, InvoiceId, FlagDate, DaysOverdue, OverdueAmount, SeverityCode, StatusCode, ResolvedDate, Notes, AssignedToUserId, CreatedDateUtc", "Notes LIKE '%' + @SearchTerm + '%' OR SeverityCode LIKE '%' + @SearchTerm + '%'", "FlagDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<DelinquencyFlagDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DelinquencyFlagDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
