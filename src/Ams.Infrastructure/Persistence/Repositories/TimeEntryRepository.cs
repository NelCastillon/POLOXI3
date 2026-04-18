using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TimeEntryRepository : ITimeEntryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public TimeEntryRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<TimeEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT TimeEntryId, TenantId, EngagementId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, InvoiceId, CreatedDateUtc FROM Billing.TimeEntry WHERE TimeEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TimeEntryDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TimeEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.TimeEntry", "TimeEntryId, TenantId, EngagementId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, InvoiceId, CreatedDateUtc", "Description LIKE '%' + @SearchTerm + '%'", "EntryDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TimeEntryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TimeEntryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
