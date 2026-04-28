-- ============================================================================
-- Finance Management and Reporting Queries
-- Description: Utility queries for managing and reporting on Finance data
-- ============================================================================

-- Demo TenantId
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

PRINT '======================================================================';
PRINT 'Finance Management and Reporting Queries';
PRINT '======================================================================';
PRINT '';

-- ============================================================================
-- 1. GL Account Summary
-- ============================================================================
PRINT '1. GL ACCOUNT SUMMARY';
PRINT '---';

SELECT 
    AccountTypeCode,
    COUNT(*) AS AccountCount,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCount,
    SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END) AS InactiveCount
FROM Finance.GLAccount
WHERE TenantId = @TenantId AND IsDeleted = 0
GROUP BY AccountTypeCode
ORDER BY AccountTypeCode;

PRINT '';

-- ============================================================================
-- 2. Vendor Summary
-- ============================================================================
PRINT '2. VENDOR SUMMARY';
PRINT '---';

SELECT 
    VendorTypeCode,
    StatusCode,
    COUNT(*) AS VendorCount,
    COUNT(DISTINCT Email) AS WithEmail,
    COUNT(DISTINCT Phone) AS WithPhone
FROM Finance.Vendor
WHERE TenantId = @TenantId AND IsDeleted = 0
GROUP BY VendorTypeCode, StatusCode
ORDER BY VendorTypeCode, StatusCode;

PRINT '';

-- ============================================================================
-- 3. Outstanding AP Invoices
-- ============================================================================
PRINT '3. OUTSTANDING AP INVOICES (Open and Partially Paid)';
PRINT '---';

SELECT 
    v.VendorName,
    i.InvoiceNumber,
    i.InvoiceDate,
    i.DueDate,
    i.Amount,
    i.AmountPaid,
    (i.Amount - i.AmountPaid) AS OutstandingAmount,
    DATEDIFF(DAY, i.DueDate, GETUTCDATE()) AS DaysOverdue,
    i.StatusCode
FROM Finance.ApInvoice i
    INNER JOIN Finance.Vendor v ON i.VendorId = v.VendorId
WHERE i.TenantId = @TenantId 
    AND i.IsDeleted = 0 
    AND i.StatusCode IN ('Open', 'PartiallyPaid')
ORDER BY i.DueDate ASC;

PRINT '';

-- ============================================================================
-- 4. AP Aging Report
-- ============================================================================
PRINT '4. AP AGING REPORT';
PRINT '---';

SELECT 
    CASE 
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 0 THEN 'Not Due'
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 30 THEN '1-30 Days'
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 60 THEN '31-60 Days'
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 90 THEN '61-90 Days'
        ELSE 'Over 90 Days'
    END AS AgingBucket,
    COUNT(*) AS InvoiceCount,
    SUM(i.Amount - i.AmountPaid) AS TotalOutstanding
FROM Finance.ApInvoice i
WHERE i.TenantId = @TenantId 
    AND i.IsDeleted = 0 
    AND i.StatusCode IN ('Open', 'PartiallyPaid')
GROUP BY 
    CASE 
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 0 THEN 'Not Due'
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 30 THEN '1-30 Days'
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 60 THEN '31-60 Days'
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 90 THEN '61-90 Days'
        ELSE 'Over 90 Days'
    END
ORDER BY 
    CASE 
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 0 THEN 0
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 30 THEN 1
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 60 THEN 2
        WHEN DATEDIFF(DAY, i.DueDate, GETUTCDATE()) <= 90 THEN 3
        ELSE 4
    END;

PRINT '';

-- ============================================================================
-- 5. Vendor Payment History
-- ============================================================================
PRINT '5. VENDOR PAYMENT HISTORY (Last 10 Payments)';
PRINT '---';

SELECT TOP 10
    v.VendorName,
    p.PaymentDate,
    p.Amount,
    p.PaymentMethodCode,
    p.ReferenceNumber,
    p.StatusCode
FROM Finance.ApPayment p
    INNER JOIN Finance.Vendor v ON p.VendorId = v.VendorId
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
ORDER BY p.PaymentDate DESC;

PRINT '';

-- ============================================================================
-- 6. Payment Pending Completion
-- ============================================================================
PRINT '6. PAYMENTS PENDING COMPLETION';
PRINT '---';

SELECT 
    v.VendorName,
    p.PaymentDate,
    p.Amount,
    p.PaymentMethodCode,
    p.ReferenceNumber,
    DATEDIFF(DAY, p.PaymentDate, GETUTCDATE()) AS DaysSinceDated
FROM Finance.ApPayment p
    INNER JOIN Finance.Vendor v ON p.VendorId = v.VendorId
WHERE p.TenantId = @TenantId 
    AND p.IsDeleted = 0 
    AND p.StatusCode = 'Pending'
ORDER BY p.PaymentDate ASC;

PRINT '';

-- ============================================================================
-- 7. Journal Entry Summary
-- ============================================================================
PRINT '7. JOURNAL ENTRY SUMMARY';
PRINT '---';

SELECT 
    EntryDate,
    StatusCode,
    COUNT(*) AS EntryCount,
    SUM(TotalDebit) AS TotalDebits,
    SUM(TotalCredit) AS TotalCredits,
    SUM(TotalDebit) - SUM(TotalCredit) AS BalanceCheck
FROM Finance.JournalEntry
WHERE TenantId = @TenantId AND IsDeleted = 0
GROUP BY EntryDate, StatusCode
ORDER BY EntryDate DESC;

PRINT '';

-- ============================================================================
-- 8. Bank Reconciliation Status
-- ============================================================================
PRINT '8. BANK RECONCILIATION STATUS';
PRINT '---';

SELECT 
    BankAccountNumber,
    BankName,
    BankStatementDate,
    BankBalance,
    BookBalance,
    OutstandingDeposits,
    OutstandingChecks,
    Discrepancy,
    StatusCode,
    CASE 
        WHEN Discrepancy = 0 THEN 'Balanced'
        WHEN Discrepancy > 0 THEN 'Variance: +' + CAST(ABS(Discrepancy) AS VARCHAR(20))
        ELSE 'Variance: -' + CAST(ABS(Discrepancy) AS VARCHAR(20))
    END AS BalanceStatus
FROM Finance.BankReconciliation
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY BankStatementDate DESC;

PRINT '';

-- ============================================================================
-- 9. Accounting Periods
-- ============================================================================
PRINT '9. ACCOUNTING PERIODS';
PRINT '---';

SELECT 
    PeriodCode,
    PeriodName,
    StartDate,
    EndDate,
    StatusCode,
    DATEDIFF(DAY, StartDate, EndDate) + 1 AS DaysInPeriod,
    CASE 
        WHEN GETUTCDATE() BETWEEN StartDate AND EndDate THEN 'Current'
        WHEN GETUTCDATE() > EndDate THEN 'Closed'
        ELSE 'Future'
    END AS PeriodStatus
FROM Finance.AccountingPeriod
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY StartDate DESC;

PRINT '';

-- ============================================================================
-- 10. Total AP by Vendor
-- ============================================================================
PRINT '10. TOTAL AP BY VENDOR (Outstanding Invoices)';
PRINT '---';

SELECT 
    v.VendorCode,
    v.VendorName,
    COUNT(i.ApInvoiceId) AS InvoiceCount,
    SUM(i.Amount) AS TotalInvoiced,
    SUM(i.AmountPaid) AS TotalPaid,
    SUM(i.Amount - i.AmountPaid) AS OutstandingBalance
FROM Finance.Vendor v
    LEFT JOIN Finance.ApInvoice i ON v.VendorId = i.VendorId 
        AND i.TenantId = @TenantId 
        AND i.IsDeleted = 0 
        AND i.StatusCode IN ('Open', 'PartiallyPaid')
WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
GROUP BY v.VendorCode, v.VendorName
HAVING SUM(i.Amount - i.AmountPaid) > 0
ORDER BY SUM(i.Amount - i.AmountPaid) DESC;

PRINT '';

-- ============================================================================
-- 11. Invoice Status Distribution
-- ============================================================================
PRINT '11. INVOICE STATUS DISTRIBUTION';
PRINT '---';

SELECT 
    StatusCode,
    COUNT(*) AS InvoiceCount,
    SUM(Amount) AS TotalAmount,
    SUM(AmountPaid) AS TotalPaid,
    SUM(Amount - AmountPaid) AS OutstandingAmount,
    CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() AS DECIMAL(5,1)) AS PercentOfTotal
FROM Finance.ApInvoice
WHERE TenantId = @TenantId AND IsDeleted = 0
GROUP BY StatusCode
ORDER BY InvoiceCount DESC;

PRINT '';

-- ============================================================================
-- 12. Payment Method Usage
-- ============================================================================
PRINT '12. PAYMENT METHOD USAGE (Last 30 Days)';
PRINT '---';

SELECT 
    PaymentMethodCode,
    COUNT(*) AS PaymentCount,
    SUM(Amount) AS TotalAmount,
    AVG(Amount) AS AverageAmount
FROM Finance.ApPayment
WHERE TenantId = @TenantId 
    AND IsDeleted = 0 
    AND PaymentDate >= CAST(DATEADD(DAY, -30, GETUTCDATE()) AS DATE)
GROUP BY PaymentMethodCode
ORDER BY PaymentCount DESC;

PRINT '';

-- ============================================================================
-- 13. Data Quality Check
-- ============================================================================
PRINT '13. DATA QUALITY CHECK';
PRINT '---';

SELECT 
    'GL Accounts' AS Entity,
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS DeletedRecords,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveRecords
FROM Finance.GLAccount
WHERE TenantId = @TenantId

UNION ALL

SELECT 
    'Vendors' AS Entity,
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS DeletedRecords,
    SUM(CASE WHEN StatusCode = 'Active' THEN 1 ELSE 0 END) AS ActiveRecords
FROM Finance.Vendor
WHERE TenantId = @TenantId

UNION ALL

SELECT 
    'AP Invoices' AS Entity,
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS DeletedRecords,
    SUM(CASE WHEN StatusCode = 'Paid' THEN 1 ELSE 0 END) AS ActiveRecords
FROM Finance.ApInvoice
WHERE TenantId = @TenantId

UNION ALL

SELECT 
    'AP Payments' AS Entity,
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS DeletedRecords,
    SUM(CASE WHEN StatusCode = 'Completed' THEN 1 ELSE 0 END) AS ActiveRecords
FROM Finance.ApPayment
WHERE TenantId = @TenantId

UNION ALL

SELECT 
    'Journal Entries' AS Entity,
    COUNT(*) AS TotalRecords,
    SUM(CASE WHEN IsDeleted = 1 THEN 1 ELSE 0 END) AS DeletedRecords,
    SUM(CASE WHEN StatusCode = 'Posted' THEN 1 ELSE 0 END) AS ActiveRecords
FROM Finance.JournalEntry
WHERE TenantId = @TenantId;

PRINT '';
PRINT '======================================================================';
PRINT '✓ Query execution completed';
PRINT '======================================================================';
