using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ArAgingSnapshotRepository : IArAgingSnapshotRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ArAgingSnapshotRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ArAgingSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc FROM Billing.ArAgingSnapshot WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ArAgingSnapshotDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ArAgingSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Billing.ArAgingSnapshot", "SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc", "CAST(AccountId AS NVARCHAR(50)) LIKE '%' + @SearchTerm + '%'", "SnapshotDate DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ArAgingSnapshotDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ArAgingSnapshotDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateArAgingSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var total = request.CurrentAmount + request.Days30Amount + request.Days60Amount + request.Days90Amount + request.Days90PlusAmount;
        const string sql = @"
INSERT INTO Billing.ArAgingSnapshot (SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @SnapshotDate, @CurrentAmount, @Days30Amount, @Days60Amount, @Days90Amount, @Days90PlusAmount, @TotalOutstanding, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.SnapshotDate, request.CurrentAmount, request.Days30Amount, request.Days60Amount, request.Days90Amount, request.Days90PlusAmount, TotalOutstanding = total, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateArAgingSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var total = request.CurrentAmount + request.Days30Amount + request.Days60Amount + request.Days90Amount + request.Days90PlusAmount;
        const string sql = @"
UPDATE Billing.ArAgingSnapshot
SET AccountId = @AccountId,
    SnapshotDate = @SnapshotDate,
    CurrentAmount = @CurrentAmount,
    Days30Amount = @Days30Amount,
    Days60Amount = @Days60Amount,
    Days90Amount = @Days90Amount,
    Days90PlusAmount = @Days90PlusAmount,
    TotalOutstanding = @TotalOutstanding,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AccountId, request.SnapshotDate, request.CurrentAmount, request.Days30Amount, request.Days60Amount, request.Days90Amount, request.Days90PlusAmount, TotalOutstanding = total, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Billing.ArAgingSnapshot SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE SnapshotId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<int> SyncFromInvoicesAsync(Guid tenantId, DateOnly snapshotDate, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Billing.ArAgingSnapshot
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId
WHERE TenantId = @TenantId AND SnapshotDate = @SnapshotDate AND IsDeleted = 0;

INSERT INTO Billing.ArAgingSnapshot (SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(),
       i.TenantId,
       i.AccountId,
       @SnapshotDate,
       SUM(CASE WHEN DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) <= 0 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) BETWEEN 1 AND 30 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) BETWEEN 31 AND 60 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) BETWEEN 61 AND 90 THEN i.BalanceAmount ELSE 0 END),
       SUM(CASE WHEN DATEDIFF(day, CAST(i.DueDate AS date), @SnapshotDate) > 90 THEN i.BalanceAmount ELSE 0 END),
       SUM(i.BalanceAmount),
       SYSUTCDATETIME(),
       @CreatedByUserId,
       0
FROM Billing.Invoice i
WHERE i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.BalanceAmount > 0
GROUP BY i.TenantId, i.AccountId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, SnapshotDate = snapshotDate, CreatedByUserId = createdByUserId }, cancellationToken: cancellationToken));
    }
}
