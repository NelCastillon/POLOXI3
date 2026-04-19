using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Subscriptions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SubscriptionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = """
        s.SubscriptionId, s.TenantId, t.TenantName, s.PlanId, p.PlanCode,
        s.StatusCode, s.RenewalType, s.BillingCycle, s.BaseAmount,
        s.StartDateUtc, s.EndDateUtc, s.CreatedDateUtc, s.ModifiedDateUtc
        """;

    private const string FromJoins = """
        FROM Commercial.Subscription s
        INNER JOIN Core.Tenant          t ON t.TenantId = s.TenantId
        INNER JOIN Commercial.[Plan]      p ON p.PlanId   = s.PlanId
        """;

    public async Task<SubscriptionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} {FromJoins} WHERE s.SubscriptionId = @Id AND s.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SubscriptionDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<SubscriptionDto>> SearchAsync(string? searchTerm = null, Guid? tenantId = null, Guid? planId = null, string? statusCode = null, string? renewalType = null, string? billingCycle = null, bool? pastDue = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var whereCore = """
            s.IsDeleted = 0
              AND (@TenantId    IS NULL OR s.TenantId    = @TenantId)
              AND (@PlanId      IS NULL OR s.PlanId      = @PlanId)
              AND (@StatusCode  IS NULL OR @StatusCode  = '' OR s.StatusCode  = @StatusCode)
              AND (@RenewalType IS NULL OR @RenewalType = '' OR s.RenewalType = @RenewalType)
              AND (@BillingCycle IS NULL OR @BillingCycle = '' OR s.BillingCycle = @BillingCycle)
              AND (@PastDue IS NULL OR (@PastDue = 1 AND s.EndDateUtc < SYSUTCDATETIME() AND s.StatusCode NOT IN ('Cancelled','Expired')))
              AND (@SearchTerm IS NULL OR @SearchTerm = ''
                   OR t.TenantName LIKE '%' + @SearchTerm + '%'
                   OR p.PlanCode   LIKE '%' + @SearchTerm + '%')
            """;
        var sql = $"""
            ;WITH Cte AS (
                SELECT {SelectColumns}
                {FromJoins}
                WHERE {whereCore}
            )
            SELECT * FROM Cte ORDER BY StartDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1)
            FROM Commercial.Subscription s
            INNER JOIN Core.Tenant     t ON t.TenantId = s.TenantId
            INNER JOIN Commercial.[Plan] p ON p.PlanId   = s.PlanId
            WHERE {whereCore};
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, TenantId = tenantId, PlanId = planId, StatusCode = statusCode, RenewalType = renewalType, BillingCycle = billingCycle, PastDue = pastDue.HasValue ? (pastDue.Value ? 1 : (int?)null) : (int?)null, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SubscriptionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SubscriptionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Commercial.Subscription
                (SubscriptionId, TenantId, PlanId, StatusCode, RenewalType, BillingCycle,
                 BaseAmount, StartDateUtc, EndDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@SubscriptionId, @TenantId, @PlanId, 'Active', @RenewalType, @BillingCycle,
                 @BaseAmount, @StartDateUtc, @EndDateUtc, SYSUTCDATETIME(), @CreatedByUserId, 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubscriptionId  = id,
            request.TenantId,
            request.PlanId,
            request.RenewalType,
            request.BillingCycle,
            request.BaseAmount,
            request.StartDateUtc,
            request.EndDateUtc,
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpgradeAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Commercial.Subscription SET
                PlanId          = @NewPlanId,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE SubscriptionId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, NewPlanId = newPlanId }, cancellationToken: cancellationToken));
    }

    public async Task DowngradeAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default)
        => await UpgradeAsync(id, newPlanId, cancellationToken);

    public async Task RenewAsync(Guid id, DateTime newEndDateUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Commercial.Subscription SET
                StatusCode      = 'Active',
                EndDateUtc      = @NewEndDateUtc,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE SubscriptionId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, NewEndDateUtc = newEndDateUtc }, cancellationToken: cancellationToken));
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Commercial.Subscription SET
                StatusCode      = 'Cancelled',
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE SubscriptionId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Commercial.Subscription SET
                IsDeleted       = 1,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE SubscriptionId = @Id;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
