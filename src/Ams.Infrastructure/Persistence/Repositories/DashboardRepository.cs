using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public DashboardRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<DashboardKpiDto> GetKpiAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    (SELECT COUNT(1) FROM Core.Tenant WHERE IsDeleted = 0)                                                           AS TotalTenants,
    (SELECT COUNT(1) FROM IAM.[User]          WHERE TenantId = @TenantId AND IsDeleted = 0)                          AS TotalUsers,
    (SELECT COUNT(1) FROM CRM.Lead            WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId = 1)     AS OpenLeads,
    (SELECT COUNT(1) FROM Client.Account      WHERE TenantId = @TenantId AND IsDeleted = 0)                          AS ActiveAccounts,
    (SELECT COUNT(1) FROM CRM.Opportunity     WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId = 1)     AS OpenOpportunities,
    (SELECT COUNT(1) FROM OPS.Engagement      WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = 'Active') AS ActiveEngagements,
    (SELECT ISNULL(SUM(BalanceAmount),0) FROM Finance.Invoice WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId NOT IN (3,4)) AS OutstandingInvoicesAmount,
    (SELECT ISNULL(SUM(Amount),0)        FROM Billing.Payment  WHERE TenantId = @TenantId AND IsDeleted = 0 AND PaymentDate >= DATEFROMPARTS(YEAR(GETUTCDATE()),MONTH(GETUTCDATE()),1)) AS CollectedThisMonthAmount,
    (SELECT COUNT(1) FROM Workflow.ApprovalStep WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = 'Pending') AS PendingApprovals,
    (SELECT COUNT(1) FROM OPS.IssueTracker  WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = 'Open')    AS OpenIssues,
    (SELECT ISNULL(SUM(CommissionAmount),0) FROM Commission.CommissionTransaction WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = 'Pending') AS PendingCommissionsAmount,
    (SELECT COUNT(1) FROM DMS.Document      WHERE TenantId = @TenantId AND IsDeleted = 0)                            AS TotalDocuments;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<DashboardKpiDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
