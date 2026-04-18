using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RecurringBillingScheduleRepository : IRecurringBillingScheduleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public RecurringBillingScheduleRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RecurringBillingScheduleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ScheduleId, TenantId, AccountId, AgreementId, ScheduleName, FrequencyCode, BillingAmount, StartDate, EndDate, NextBillingDate, LastBillingDate, StatusCode, Description, CreatedDateUtc FROM Billing.RecurringBillingSchedule WHERE ScheduleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RecurringBillingScheduleDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RecurringBillingScheduleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.RecurringBillingSchedule", "ScheduleId, TenantId, AccountId, AgreementId, ScheduleName, FrequencyCode, BillingAmount, StartDate, EndDate, NextBillingDate, LastBillingDate, StatusCode, Description, CreatedDateUtc", "ScheduleName LIKE '%' + @SearchTerm + '%'", "NextBillingDate ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RecurringBillingScheduleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RecurringBillingScheduleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
