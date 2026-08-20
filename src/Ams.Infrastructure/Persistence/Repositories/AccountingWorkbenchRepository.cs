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
    w.PolicyAccountingWorkItemId AS ItemId,
    w.QueueCode,
    w.Title,
    w.ReferenceNumber AS RefNumber,
    COALESCE(a.AccountName, '') AS AccountName,
    COALESCE(bp.PolicyNumber, '') AS PolicyNumber,
    COALESCE(c.CarrierName, '') AS CarrierName,
    COALESCE(u.FullName, u.DisplayName, '') AS ProducerName,
    CASE WHEN w.QueueCode = 'new-policy-billing' THEN 'New Policies Awaiting Billing' WHEN w.QueueCode = 'accounting-failures' THEN 'Accounting Exception' ELSE 'Policy Accounting' END AS Category,
    COALESCE(au.FullName, au.DisplayName, 'Unassigned') AS AssignedTo,
    w.PriorityCode AS Priority,
    CASE WHEN w.DueDateUtc < SYSUTCDATETIME() THEN 'Breached' WHEN w.DueDateUtc < DATEADD(day, 1, SYSUTCDATETIME()) THEN 'At Risk' ELSE 'On Track' END AS SlaStatus,
    w.StatusCode AS Status,
    CASE WHEN w.DueDateUtc < SYSUTCDATETIME() THEN 'Past Due' ELSE 'Current' END AS AgingBucket,
    '' AS PaymentMethod,
    w.WorkItemTypeCode AS Reason,
    w.Amount,
    CAST(0 AS DECIMAL(18,2)) AS Variance,
    COALESCE(w.DueDateUtc, w.CreatedDateUtc) AS DueDate,
    NULL AS ReceivedDate,
    w.CompletedDateUtc AS CompletedAt,
    CASE WHEN w.DueDateUtc IS NULL THEN 0 ELSE DATEDIFF(day, w.DueDateUtc, SYSUTCDATETIME()) END AS AgeDays,
    w.Notes,
    COALESCE(NULLIF(w.DetailUrl, ''), CONCAT('/policies/', CONVERT(NVARCHAR(36), w.PolicyId))) AS DetailUrl
FROM Accounting.PolicyAccountingWorkItem w
JOIN Submissions.BoundPolicy bp ON bp.TenantId=w.TenantId AND bp.PolicyId=w.PolicyId AND bp.IsDeleted=0
LEFT JOIN Client.Account a ON a.TenantId=bp.TenantId AND a.AccountId=bp.AccountId AND a.IsDeleted=0
LEFT JOIN Agency.Carrier c ON c.TenantId=bp.TenantId AND c.CarrierId=bp.CarrierId AND c.IsDeleted=0
LEFT JOIN Policy.PolicyAssignment pa ON pa.TenantId=w.TenantId AND pa.PolicyId=w.PolicyId AND pa.IsDeleted=0
LEFT JOIN IAM.[User] u ON u.TenantId=w.TenantId AND u.UserId=pa.ProducerId AND u.IsDeleted=0
LEFT JOIN IAM.[User] au ON au.TenantId=w.TenantId AND au.UserId=w.AssignedToUserId AND au.IsDeleted=0
WHERE w.TenantId=@TenantId AND w.IsDeleted=0

UNION ALL SELECT i.InvoiceId,'invoices-due',CONCAT('Invoice due - ',i.InvoiceNumber),i.InvoiceNumber,COALESCE(a.AccountName,''),COALESCE(bp.PolicyNumber,''),'','','Invoice','Unassigned',CASE WHEN i.DueDate<CONVERT(date,SYSUTCDATETIME()) THEN 'High' ELSE 'Normal' END,CASE WHEN i.DueDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Breached' ELSE 'On Track' END,i.StatusCode,CASE WHEN i.DueDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Past Due' ELSE 'Current' END,'','Open invoice balance',i.BalanceAmount,0,CONVERT(datetime2,i.DueDate),NULL,NULL,DATEDIFF(day,i.DueDate,CONVERT(date,SYSUTCDATETIME())),'Persisted policy invoice requires collection or review.',CONCAT('/policies/',CONVERT(nvarchar(36),i.PolicyId)) FROM Billing.Invoice i LEFT JOIN Client.Account a ON a.TenantId=i.TenantId AND a.AccountId=i.AccountId AND a.IsDeleted=0 LEFT JOIN Submissions.BoundPolicy bp ON bp.TenantId=i.TenantId AND bp.PolicyId=i.PolicyId AND bp.IsDeleted=0 WHERE i.TenantId=@TenantId AND i.IsDeleted=0 AND i.BalanceAmount>0 AND i.StatusCode NOT IN('Paid','Void','Cancelled')
UNION ALL SELECT cp.CarrierPayableId,'carrier-remittance',CONCAT('Carrier remittance - ',cp.PayableNumber),cp.PayableNumber,COALESCE(a.AccountName,''),COALESCE(bp.PolicyNumber,''),COALESCE(c.CarrierName,''),'','Carrier Payable','Unassigned',CASE WHEN cp.DueDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Critical' ELSE 'High' END,CASE WHEN cp.DueDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Breached' ELSE 'At Risk' END,cp.StatusCode,CASE WHEN cp.DueDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Past Due' ELSE 'Current' END,'','Premium trust remittance due',cp.PayableAmount-cp.PaidAmount,0,CONVERT(datetime2,cp.DueDate),NULL,cp.RemittedDateUtc,DATEDIFF(day,cp.DueDate,CONVERT(date,SYSUTCDATETIME())),'Remit cleared premium trust funds to the carrier.',CONCAT('/policies/',CONVERT(nvarchar(36),cp.PolicyId)) FROM Accounting.CarrierPayable cp JOIN Submissions.BoundPolicy bp ON bp.TenantId=cp.TenantId AND bp.PolicyId=cp.PolicyId AND bp.IsDeleted=0 LEFT JOIN Client.Account a ON a.TenantId=bp.TenantId AND a.AccountId=bp.AccountId AND a.IsDeleted=0 LEFT JOIN Agency.Carrier c ON c.TenantId=cp.TenantId AND c.CarrierId=cp.CarrierId AND c.IsDeleted=0 WHERE cp.TenantId=@TenantId AND cp.IsDeleted=0 AND cp.PayableAmount>cp.PaidAmount AND cp.StatusCode<>'Remitted'
UNION ALL SELECT p.PaymentId,'failed-payment',CONCAT('Failed payment - ',COALESCE(p.PaymentNumber,CONVERT(nvarchar(36),p.PaymentId))),COALESCE(p.PaymentNumber,CONVERT(nvarchar(36),p.PaymentId)),COALESCE(a.AccountName,''),'','','','Payment','Unassigned','High','At Risk',p.StatusCode,'Current',p.PaymentMethodCode,'Payment processing failed',p.Amount,0,p.PaymentDate,NULL,NULL,DATEDIFF(day,p.PaymentDate,SYSUTCDATETIME()),COALESCE(p.Notes,'Payment requires exception review.'),'/billing/payments' FROM Billing.Payment p LEFT JOIN Client.Account a ON a.TenantId=p.TenantId AND a.AccountId=p.AccountId AND a.IsDeleted=0 WHERE p.TenantId=@TenantId AND p.IsDeleted=0 AND p.StatusCode IN('Failed','Declined','Rejected','Returned')
UNION ALL SELECT fa.FinanceAgreementId,'premium-finance',CONCAT('Premium finance funding - ',fa.AgreementNumber),fa.AgreementNumber,COALESCE(a.AccountName,''),COALESCE(bp.PolicyNumber,''),'','','Premium Finance','Unassigned',CASE WHEN fa.ExpectedFundingDate<CONVERT(date,SYSUTCDATETIME()) THEN 'High' ELSE 'Normal' END,CASE WHEN fa.ExpectedFundingDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Breached' ELSE 'On Track' END,fa.FundingStatusCode,CASE WHEN fa.ExpectedFundingDate<CONVERT(date,SYSUTCDATETIME()) THEN 'Past Due' ELSE 'Current' END,'','Funding confirmation pending',fa.FinancedAmount,0,CONVERT(datetime2,COALESCE(fa.ExpectedFundingDate,CONVERT(date,fa.CreatedDateUtc))),NULL,NULL,DATEDIFF(day,COALESCE(fa.ExpectedFundingDate,CONVERT(date,fa.CreatedDateUtc)),CONVERT(date,SYSUTCDATETIME())),'Confirm premium finance funding and cancellation protection.',CONCAT('/policies/',CONVERT(nvarchar(36),r.PolicyId)) FROM Billing.FinanceAgreement fa JOIN Billing.AgencyBillReceivable r ON r.TenantId=fa.TenantId AND r.AgencyBillReceivableId=fa.AgencyBillReceivableId AND r.IsDeleted=0 LEFT JOIN Client.Account a ON a.TenantId=r.TenantId AND a.AccountId=r.AccountId AND a.IsDeleted=0 LEFT JOIN Submissions.BoundPolicy bp ON bp.TenantId=r.TenantId AND bp.PolicyId=r.PolicyId AND bp.IsDeleted=0 WHERE fa.TenantId=@TenantId AND fa.IsDeleted=0 AND fa.StatusCode='Active' AND fa.FundingStatusCode='Pending'
UNION ALL SELECT cp.CommissionPayableId,'commission-approval',CONCAT('Commission approval - ',cp.PayableNumber),cp.PayableNumber,'','','','','Commission Payable','Unassigned','Normal','On Track',cp.StatusCode,'Current','','Commission payable pending approval',cp.NetPayableAmount,0,CONVERT(datetime2,cp.AccountingDate),NULL,NULL,DATEDIFF(day,cp.AccountingDate,CONVERT(date,SYSUTCDATETIME())),'Approve reconciled commission payable for payout.','/commissions/accounting' FROM Commission.CommissionPayable cp WHERE cp.TenantId=@TenantId AND cp.IsDeleted=0 AND cp.StatusCode='PendingApproval'
ORDER BY DueDate, Priority;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<AccountingWorkbenchItemDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();

        var reconciliation = items.Where(i => i.QueueCode == "reconciliation").ToList();
        var arAging = items.Where(i => i.QueueCode == "ar-aging").ToList();
        var unapplied = items.Where(i => i.QueueCode == "unapplied-payments").ToList();
        var commission = items.Where(i => i.QueueCode == "commission-adj").ToList();
        var directBill = items.Where(i => i.QueueCode == "direct-bill").ToList();
        var monthEnd = items.Where(i => i.QueueCode == "month-end").ToList();
        var newPolicyBilling = items.Where(i => i.QueueCode == "new-policy-billing").ToList();
        var accountingFailures = items.Where(i => i.QueueCode == "accounting-failures").ToList();
        var invoicesDue = items.Where(i => i.QueueCode == "invoices-due").ToList();
        var carrierRemittances = items.Where(i => i.QueueCode == "carrier-remittance").ToList();
        var failedPayments = items.Where(i => i.QueueCode == "failed-payment").ToList();
        var premiumFinance = items.Where(i => i.QueueCode == "premium-finance").ToList();
        var commissionApprovals = items.Where(i => i.QueueCode == "commission-approval").ToList();

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
                NewPoliciesAwaitingBilling = newPolicyBilling.Count(i => i.Status != "Complete"),
                NewPolicyBillingAmount = newPolicyBilling.Where(i => i.Status != "Complete").Sum(i => i.Amount),
                AccountingFailures = accountingFailures.Count(i => i.Status != "Complete"),
                InvoicesDue = invoicesDue.Count,
                InvoicesDueAmount = invoicesDue.Sum(i => i.Amount),
                CarrierRemittances = carrierRemittances.Count,
                CarrierRemittanceAmount = carrierRemittances.Sum(i => i.Amount),
                FailedPayments = failedPayments.Count,
                PremiumFinancePending = premiumFinance.Count,
                CommissionApprovals = commissionApprovals.Count,
            },
            Reconciliation = reconciliation,
            ArAging = arAging,
            UnappliedPayments = unapplied,
            CommissionAdjustments = commission,
            DirectBillExceptions = directBill,
            MonthEnd = monthEnd,
            NewPolicyBilling = newPolicyBilling,
            AccountingFailures = accountingFailures,
            InvoicesDue = invoicesDue,
            CarrierRemittances = carrierRemittances,
            FailedPayments = failedPayments,
            PremiumFinance = premiumFinance,
            CommissionApprovals = commissionApprovals,
        };
    }
}
