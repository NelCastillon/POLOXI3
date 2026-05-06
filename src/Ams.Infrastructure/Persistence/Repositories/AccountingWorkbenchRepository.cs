using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountingWorkbenchRepository : IAccountingWorkbenchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountingWorkbenchRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AccountingWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, bool teamScope, string? branchId, string? teamId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT
    ar.SnapshotId AS ItemId,
    'ar-aging' AS QueueCode,
    CONCAT('AR balance review - ', COALESCE(a.AccountName, 'Unknown Account')) AS Title,
    CONCAT('AR-', FORMAT(ar.SnapshotDate, 'yyyyMMdd'), '-', RIGHT(CONVERT(NVARCHAR(36), ar.SnapshotId), 6)) AS RefNumber,
    COALESCE(a.AccountName, 'Unknown Account') AS AccountName,
    '' AS PolicyNumber,
    '' AS CarrierName,
    '' AS ProducerName,
    'Accounts Receivable' AS Category,
    'Tenant Admin' AS AssignedTo,
    CASE WHEN ar.Days90PlusAmount > 0 OR ar.Days90Amount > 0 THEN 'Critical' WHEN ar.Days60Amount > 0 THEN 'High' WHEN ar.Days30Amount > 0 THEN 'Normal' ELSE 'Low' END AS Priority,
    CASE WHEN ar.Days90PlusAmount > 0 OR ar.Days90Amount > 0 THEN 'Breached' WHEN ar.Days60Amount > 0 THEN 'At Risk' ELSE 'On Track' END AS SlaStatus,
    'Open' AS Status,
    CASE WHEN ar.Days90PlusAmount > 0 THEN '90+' WHEN ar.Days90Amount > 0 THEN '61-90' WHEN ar.Days60Amount > 0 THEN '31-60' WHEN ar.Days30Amount > 0 THEN '1-30' ELSE 'Current' END AS AgingBucket,
    '' AS PaymentMethod,
    'Aged receivable requires collection follow-up' AS Reason,
    ar.TotalOutstanding AS Amount,
    0 AS Variance,
    CAST(ar.SnapshotDate AS DATETIME2) AS DueDate,
    NULL AS ReceivedDate,
    NULL AS CompletedAt,
    CASE WHEN ar.Days90PlusAmount > 0 THEN 95 WHEN ar.Days90Amount > 0 THEN 75 WHEN ar.Days60Amount > 0 THEN 45 WHEN ar.Days30Amount > 0 THEN 15 ELSE 0 END AS AgeDays,
    'AR aging snapshot item generated for accounting workbench.' AS Notes,
    '/billing/ar-aging' AS DetailUrl
FROM Billing.ArAgingSnapshot ar
LEFT JOIN Client.Account a ON a.AccountId = ar.AccountId
WHERE ar.TenantId = @TenantId AND ar.IsDeleted = 0 AND ar.TotalOutstanding > 0

UNION ALL

SELECT
    pr.PortalAdminRecordId AS ItemId,
    JSON_VALUE(pr.JsonData, '$.queueCode') AS QueueCode,
    pr.Name AS Title,
    pr.Code AS RefNumber,
    COALESCE(JSON_VALUE(pr.JsonData, '$.accountName'), '') AS AccountName,
    COALESCE(JSON_VALUE(pr.JsonData, '$.policyNumber'), '') AS PolicyNumber,
    COALESCE(JSON_VALUE(pr.JsonData, '$.carrierName'), '') AS CarrierName,
    COALESCE(JSON_VALUE(pr.JsonData, '$.producerName'), '') AS ProducerName,
    COALESCE(JSON_VALUE(pr.JsonData, '$.category'), '') AS Category,
    COALESCE(JSON_VALUE(pr.JsonData, '$.assignedTo'), 'Tenant Admin') AS AssignedTo,
    COALESCE(JSON_VALUE(pr.JsonData, '$.priority'), 'Normal') AS Priority,
    COALESCE(JSON_VALUE(pr.JsonData, '$.slaStatus'), 'On Track') AS SlaStatus,
    COALESCE(JSON_VALUE(pr.JsonData, '$.status'), pr.Status) AS Status,
    COALESCE(JSON_VALUE(pr.JsonData, '$.agingBucket'), 'Current') AS AgingBucket,
    COALESCE(JSON_VALUE(pr.JsonData, '$.paymentMethod'), '') AS PaymentMethod,
    COALESCE(JSON_VALUE(pr.JsonData, '$.reason'), '') AS Reason,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pr.JsonData, '$.amount')), 0) AS Amount,
    COALESCE(TRY_CONVERT(DECIMAL(18,2), JSON_VALUE(pr.JsonData, '$.variance')), 0) AS Variance,
    COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.dueDate')), pr.CreatedDateUtc) AS DueDate,
    TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.receivedDate')) AS ReceivedDate,
    TRY_CONVERT(DATETIME2, JSON_VALUE(pr.JsonData, '$.completedAt')) AS CompletedAt,
    COALESCE(TRY_CONVERT(INT, JSON_VALUE(pr.JsonData, '$.ageDays')), DATEDIFF(DAY, pr.CreatedDateUtc, SYSUTCDATETIME())) AS AgeDays,
    COALESCE(JSON_VALUE(pr.JsonData, '$.notes'), '') AS Notes,
    COALESCE(JSON_VALUE(pr.JsonData, '$.detailUrl'), '/workbench/accounting') AS DetailUrl
FROM Portal.AdminRecord pr
WHERE pr.TenantId = @TenantId
  AND pr.Kind = 'AccountingWorkbench'
  AND pr.IsDeleted = 0
ORDER BY DueDate, Priority;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<AccountingWorkbenchItemDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();

        var reconciliation = items.Where(i => i.QueueCode == "reconciliation").ToList();
        var arAging = items.Where(i => i.QueueCode == "ar-aging").ToList();
        var unapplied = items.Where(i => i.QueueCode == "unapplied-payments").ToList();
        var commission = items.Where(i => i.QueueCode == "commission-adj").ToList();
        var directBill = items.Where(i => i.QueueCode == "direct-bill").ToList();
        var monthEnd = items.Where(i => i.QueueCode == "month-end").ToList();

        return new AccountingWorkbenchDto
        {
            Counts = new AccountingWorkbenchCountsDto
            {
                ReconciliationItems = reconciliation.Count,
                ReconciliationAmount = reconciliation.Sum(i => Math.Abs(i.Variance)),
                ArOverdue = arAging.Count(i => i.AgeDays > 0),
                ArAmount = arAging.Sum(i => i.Amount),
                UnappliedPayments = unapplied.Count,
                UnappliedAmount = unapplied.Sum(i => i.Amount),
                CommissionAdj = commission.Count,
                DirectBillExceptions = directBill.Count,
                MonthEndOpen = monthEnd.Count(i => i.Status != "Complete"),
                MonthEndComplete = monthEnd.Count(i => i.Status == "Complete"),
            },
            Reconciliation = reconciliation,
            ArAging = arAging,
            UnappliedPayments = unapplied,
            CommissionAdjustments = commission,
            DirectBillExceptions = directBill,
            MonthEnd = monthEnd,
        };
    }
}
