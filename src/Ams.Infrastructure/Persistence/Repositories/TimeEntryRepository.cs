using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
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

    public async Task<Guid> CreateAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Billing.TimeEntry (TimeEntryId, TenantId, EngagementId, AccountId, UserId, EntryDate, Hours, BillableHours, RateAmount, Description, StatusCode, InvoiceId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @EngagementId, @AccountId, @UserId, @EntryDate, @Hours, @BillableHours, @RateAmount, @Description, @StatusCode, @InvoiceId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.EngagementId, request.AccountId, request.UserId, request.EntryDate, request.Hours, request.BillableHours, request.RateAmount, request.Description, request.StatusCode, request.InvoiceId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Billing.TimeEntry
SET EngagementId = @EngagementId,
    AccountId = @AccountId,
    UserId = @UserId,
    EntryDate = @EntryDate,
    Hours = @Hours,
    BillableHours = @BillableHours,
    RateAmount = @RateAmount,
    Description = @Description,
    StatusCode = @StatusCode,
    InvoiceId = @InvoiceId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TimeEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.EngagementId, request.AccountId, request.UserId, request.EntryDate, request.Hours, request.BillableHours, request.RateAmount, request.Description, request.StatusCode, request.InvoiceId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Billing.TimeEntry SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE TimeEntryId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
