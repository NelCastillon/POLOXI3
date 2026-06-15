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
DECLARE @TotalTenants INT = 0;
DECLARE @TotalUsers INT = 0;
DECLARE @OpenLeads INT = 0;
DECLARE @ActiveAccounts INT = 0;
DECLARE @OpenOpportunities INT = 0;
DECLARE @ActiveEngagements INT = 0;
DECLARE @OutstandingInvoicesAmount DECIMAL(18,2) = 0;
DECLARE @CollectedThisMonthAmount DECIMAL(18,2) = 0;
DECLARE @PendingApprovals INT = 0;
DECLARE @OpenIssues INT = 0;
DECLARE @PendingCommissionsAmount DECIMAL(18,2) = 0;
DECLARE @TotalDocuments INT = 0;

IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM Core.Tenant WHERE IsDeleted = 0;', N'@Value INT OUTPUT', @TotalTenants OUTPUT;

IF OBJECT_ID(N'IAM.User', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM IAM.[User] WHERE TenantId = @TenantId AND IsDeleted = 0;', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @TotalUsers OUTPUT;

IF OBJECT_ID(N'CRM.Lead', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM CRM.Lead WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId = 1;', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @OpenLeads OUTPUT;

IF OBJECT_ID(N'Client.Account', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0;', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @ActiveAccounts OUTPUT;

IF OBJECT_ID(N'CRM.Opportunity', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM CRM.Opportunity WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId = 1;', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @OpenOpportunities OUTPUT;

IF OBJECT_ID(N'OPS.Engagement', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM OPS.Engagement WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = N''Active'';', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @ActiveEngagements OUTPUT;

IF OBJECT_ID(N'Finance.Invoice', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = ISNULL(SUM(BalanceAmount), 0) FROM Finance.Invoice WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId NOT IN (3,4);', N'@TenantId UNIQUEIDENTIFIER, @Value DECIMAL(18,2) OUTPUT', @TenantId, @OutstandingInvoicesAmount OUTPUT;

IF OBJECT_ID(N'Billing.Payment', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = ISNULL(SUM(Amount), 0) FROM Billing.Payment WHERE TenantId = @TenantId AND IsDeleted = 0 AND PaymentDate >= DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);', N'@TenantId UNIQUEIDENTIFIER, @Value DECIMAL(18,2) OUTPUT', @TenantId, @CollectedThisMonthAmount OUTPUT;

IF OBJECT_ID(N'Workflow.ApprovalStep', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM Workflow.ApprovalStep WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = N''Pending'';', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @PendingApprovals OUTPUT;

IF OBJECT_ID(N'OPS.IssueTracker', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM OPS.IssueTracker WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = N''Open'';', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @OpenIssues OUTPUT;

IF OBJECT_ID(N'Commission.CommissionTransaction', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = ISNULL(SUM(CommissionAmount), 0) FROM Commission.CommissionTransaction WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode = N''Pending'';', N'@TenantId UNIQUEIDENTIFIER, @Value DECIMAL(18,2) OUTPUT', @TenantId, @PendingCommissionsAmount OUTPUT;

IF OBJECT_ID(N'DMS.Document', N'U') IS NOT NULL
    EXEC sp_executesql N'SELECT @Value = COUNT(1) FROM DMS.Document WHERE TenantId = @TenantId AND IsDeleted = 0;', N'@TenantId UNIQUEIDENTIFIER, @Value INT OUTPUT', @TenantId, @TotalDocuments OUTPUT;

SELECT
    @TotalTenants AS TotalTenants,
    @TotalUsers AS TotalUsers,
    @OpenLeads AS OpenLeads,
    @ActiveAccounts AS ActiveAccounts,
    @OpenOpportunities AS OpenOpportunities,
    @ActiveEngagements AS ActiveEngagements,
    @OutstandingInvoicesAmount AS OutstandingInvoicesAmount,
    @CollectedThisMonthAmount AS CollectedThisMonthAmount,
    @PendingApprovals AS PendingApprovals,
    @OpenIssues AS OpenIssues,
    @PendingCommissionsAmount AS PendingCommissionsAmount,
    @TotalDocuments AS TotalDocuments;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<DashboardKpiDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
