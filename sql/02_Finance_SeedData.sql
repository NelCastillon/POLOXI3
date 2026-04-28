-- ============================================================================
-- Finance Seed Data Script
-- Description: Populates the Finance schema with realistic enterprise-level
--              data for testing and development
-- ============================================================================

-- Set identity insert for demo tenant
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @UserId   UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';

PRINT 'Starting Finance seed data population for TenantId: ' + CAST(@TenantId AS NVARCHAR(MAX));
PRINT '======================================================================';

-- ============================================================================
-- 1. Seed General Ledger Accounts (Chart of Accounts)
-- ============================================================================
PRINT 'Seeding GL Accounts...';

IF NOT EXISTS (SELECT 1 FROM Finance.GLAccount WHERE TenantId = @TenantId AND AccountCode = '1000')
BEGIN
    INSERT INTO Finance.GLAccount (GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, IsActive, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, '1000', 'Cash - Operating Account',         'Asset',       'Primary operating cash account',                                      1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '1010', 'Cash - Payroll Account',          'Asset',       'Payroll disbursement account',                                       1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '1020', 'Cash - Petty Cash',                'Asset',       'Petty cash for miscellaneous expenses',                              1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '1200', 'Accounts Receivable',              'Asset',       'Customer invoices outstanding',                                      1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '1210', 'Allowance for Doubtful Accounts', 'Asset',       'Reserve for uncollectible accounts',                                 1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '1500', 'Fixed Assets - Equipment',         'Asset',       'Office equipment and furniture',                                     1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '1510', 'Accumulated Depreciation',        'Asset',       'Accumulated depreciation on fixed assets',                           1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2000', 'Accounts Payable',                 'Liability',   'Vendor invoices due',                                                1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2100', 'Payroll Liabilities',              'Liability',   'Accrued payroll and withholdings',                                    1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2200', 'Deferred Revenue',                 'Liability',   'Customer prepayments',                                               1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '3000', 'Common Stock',                     'Equity',      'Shareholders equity - common stock',                                 1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '3100', 'Retained Earnings',                'Equity',      'Accumulated profit/loss',                                            1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '4000', 'Service Revenue',                  'Revenue',     'Core service revenue',                                               1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '4100', 'Consulting Revenue',               'Revenue',     'Professional consulting services',                                   1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5000', 'Salaries and Wages',               'Expense',     'Employee compensation',                                              1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5100', 'Employee Benefits',                'Expense',     'Health insurance, 401k, etc.',                                       1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5200', 'Rent Expense',                     'Expense',     'Office lease expenses',                                              1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5300', 'Utilities Expense',                'Expense',     'Electric, water, internet, phone',                                   1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5400', 'Depreciation Expense',             'Expense',     'Depreciation on fixed assets',                                       1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5500', 'Office Supplies',                  'Expense',     'Office supplies and equipment under $500',                           1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5600', 'Marketing and Advertising',       'Expense',     'Marketing campaigns and advertising',                                1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5700', 'Travel and Meals',                 'Expense',     'Employee travel and entertainment',                                  1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5800', 'Professional Services',            'Expense',     'Legal, accounting, consulting services',                             1, 0, GETUTCDATE()),
        (NEWID(), @TenantId, '5900', 'Bad Debt Expense',                 'Expense',     'Write-off of uncollectible accounts',                                1, 0, GETUTCDATE());

    PRINT '  ✓ GL Accounts seeded (24 accounts)';
END
ELSE
    PRINT '  ℹ GL Accounts already exist, skipping...';
GO

-- ============================================================================
-- 2. Seed Vendors
-- ============================================================================
PRINT 'Seeding Vendors...';

IF NOT EXISTS (SELECT 1 FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-001')
BEGIN
    INSERT INTO Finance.Vendor (VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, 'VEN-001', 'Office Supplies Inc.', 'John Smith',       'contact@officesupplies.com',     '+1 800 555 0101', 'Net30',  'USD', '12-3456789',  'Supplier',     'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-002', 'Global Tech Solutions', 'Maria Garcia',      'ap@globaltech.io',               '+1 408 555 0198', 'Net60',  'USD', '98-7654321',  'Technology',   'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-003', 'Premium Property Management', 'David Kim',     'landlord@premiumpm.com',         '+1 213 555 0156', 'Net15',  'USD', '45-6789012',  'Landlord',     'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-004', 'Professional Staffing Group', 'Angela Rodriguez','billing@profstaff.com',          '+1 305 555 0167', 'Net45',  'USD', '56-7890123',  'Contractor',   'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-005', 'Corporate Legal Associates',  'Michael Brown',  'accounting@corporatelegal.com', '+1 202 555 0144', 'Net30',  'USD', '67-8901234',  'Professional',  'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-006', 'Energy Systems LLC',          'Patricia White',  'vendor@energysys.com',            '+1 512 555 0115', 'Net30',  'USD', '78-9012345',  'Utility',      'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-007', 'Premium Benefits Broker',     'James Wilson',    'vp@benefitsbroker.com',          '+1 214 555 0182', 'Due15',  'USD', '89-0123456',  'Insurance',    'Active', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'VEN-008', 'Cloud Infrastructure Partners', 'Sarah Lee',     'billing@cloudinfra.com',         '+1 206 555 0199', 'Net45',  'USD', '90-1234567',  'Technology',   'Active', 0, GETUTCDATE());

    PRINT '  ✓ Vendors seeded (8 vendors)';
END
ELSE
    PRINT '  ℹ Vendors already exist, skipping...';
GO

-- ============================================================================
-- 3. Seed AP Invoices
-- ============================================================================
PRINT 'Seeding AP Invoices...';

IF NOT EXISTS (SELECT 1 FROM Finance.ApInvoice WHERE TenantId = @TenantId AND InvoiceNumber = 'INV-2024-0001')
BEGIN
    DECLARE @VendorId1 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-001');
    DECLARE @VendorId2 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-002');
    DECLARE @VendorId3 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-003');
    DECLARE @VendorId4 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-004');
    DECLARE @VendorId5 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-005');

    INSERT INTO Finance.ApInvoice (ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, Description, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, @VendorId1, 'INV-2024-0001', CAST(DATEADD(DAY, -45, GETUTCDATE()) AS DATE), CAST(DATEADD(DAY, -15, GETUTCDATE()) AS DATE), 2500.00, 2500.00, 200.00, 'Office supplies and equipment', 'Paid', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId2, 'INV-2024-0002', CAST(DATEADD(DAY, -30, GETUTCDATE()) AS DATE), CAST(DATEADD(DAY, 15, GETUTCDATE()) AS DATE),  8750.00, 0.00,    700.00, 'Software licenses and cloud services', 'Open', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId3, 'INV-2024-0003', CAST(DATEADD(DAY, -20, GETUTCDATE()) AS DATE), CAST(GETUTCDATE() AS DATE),              15000.00, 0.00,   0.00,   'March office rent', 'Open', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId4, 'INV-2024-0004', CAST(DATEADD(DAY, -10, GETUTCDATE()) AS DATE), CAST(DATEADD(DAY, 20, GETUTCDATE()) AS DATE),  5200.00, 2600.00, 416.00, 'Contract labor for Q1 consulting', 'PartiallyPaid', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId5, 'INV-2024-0005', CAST(DATEADD(DAY, -5, GETUTCDATE()) AS DATE),  CAST(DATEADD(DAY, 25, GETUTCDATE()) AS DATE),  3500.00, 0.00,    280.00, 'Legal review of contractor agreements', 'Open', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId1, 'INV-2024-0006', CAST(GETUTCDATE() AS DATE),                   CAST(DATEADD(DAY, 30, GETUTCDATE()) AS DATE), 1200.00, 0.00,    96.00,  'Printer ink and copy paper', 'Open', 0, GETUTCDATE());

    PRINT '  ✓ AP Invoices seeded (6 invoices)';
END
ELSE
    PRINT '  ℹ AP Invoices already exist, skipping...';
GO

-- ============================================================================
-- 4. Seed AP Payments
-- ============================================================================
PRINT 'Seeding AP Payments...';

IF NOT EXISTS (SELECT 1 FROM Finance.ApPayment WHERE TenantId = @TenantId AND ReferenceNumber = 'PAY-2024-0001')
BEGIN
    DECLARE @VendorId1 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-001');
    DECLARE @VendorId4 UNIQUEIDENTIFIER = (SELECT TOP 1 VendorId FROM Finance.Vendor WHERE TenantId = @TenantId AND VendorCode = 'VEN-004');
    DECLARE @InvoiceId1 UNIQUEIDENTIFIER = (SELECT TOP 1 ApInvoiceId FROM Finance.ApInvoice WHERE TenantId = @TenantId AND InvoiceNumber = 'INV-2024-0001');
    DECLARE @InvoiceId4 UNIQUEIDENTIFIER = (SELECT TOP 1 ApInvoiceId FROM Finance.ApInvoice WHERE TenantId = @TenantId AND InvoiceNumber = 'INV-2024-0004');

    INSERT INTO Finance.ApPayment (ApPaymentId, TenantId, VendorId, ApInvoiceId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, Notes, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, @VendorId1, @InvoiceId1, CAST(DATEADD(DAY, -35, GETUTCDATE()) AS DATE), 2500.00, 'ACH', 'PAY-2024-0001', 'Full payment for office supplies',      'Completed', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId4, @InvoiceId4, CAST(DATEADD(DAY, -3, GETUTCDATE()) AS DATE),  2600.00, 'Check', 'CHK-5823', 'Partial payment on contract labor', 'Completed', 0, GETUTCDATE()),
        (NEWID(), @TenantId, @VendorId4, @InvoiceId4, CAST(DATEADD(DAY, 5, GETUTCDATE()) AS DATE),   2600.00, 'Wire', 'WIRE-202401', 'Final payment remaining balance',    'Pending', 0, GETUTCDATE());

    PRINT '  ✓ AP Payments seeded (3 payments)';
END
ELSE
    PRINT '  ℹ AP Payments already exist, skipping...';
GO

-- ============================================================================
-- 5. Seed Accounting Periods
-- ============================================================================
PRINT 'Seeding Accounting Periods...';

IF NOT EXISTS (SELECT 1 FROM Finance.AccountingPeriod WHERE TenantId = @TenantId AND PeriodCode = '2024-01')
BEGIN
    INSERT INTO Finance.AccountingPeriod (AccountingPeriodId, TenantId, PeriodCode, PeriodName, StartDate, EndDate, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, '2024-01', 'January 2024', '2024-01-01', '2024-01-31', 'Closed', 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2024-02', 'February 2024', '2024-02-01', '2024-02-29', 'Closed', 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2024-03', 'March 2024', '2024-03-01', '2024-03-31', 'Open', 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2024-04', 'April 2024', '2024-04-01', '2024-04-30', 'Open', 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2024-Q1', 'Q1 2024', '2024-01-01', '2024-03-31', 'Open', 0, GETUTCDATE()),
        (NEWID(), @TenantId, '2024-FY', 'Fiscal Year 2024', '2024-01-01', '2024-12-31', 'Open', 0, GETUTCDATE());

    PRINT '  ✓ Accounting Periods seeded (6 periods)';
END
ELSE
    PRINT '  ℹ Accounting Periods already exist, skipping...';
GO

-- ============================================================================
-- 6. Seed Journal Entries
-- ============================================================================
PRINT 'Seeding Journal Entries...';

IF NOT EXISTS (SELECT 1 FROM Finance.JournalEntry WHERE TenantId = @TenantId AND EntryNumber = 'JE-2024-0001')
BEGIN
    INSERT INTO Finance.JournalEntry (JournalEntryId, TenantId, EntryNumber, EntryDate, Description, TotalDebit, TotalCredit, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, 'JE-2024-0001', CAST(DATEADD(DAY, -30, GETUTCDATE()) AS DATE), 'January service revenue recognized',  15000.00, 15000.00, 'Posted', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'JE-2024-0002', CAST(DATEADD(DAY, -15, GETUTCDATE()) AS DATE), 'Accrual of January payroll expenses',  28500.00, 28500.00, 'Posted', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'JE-2024-0003', CAST(DATEADD(DAY, -5, GETUTCDATE()) AS DATE),  'Record accounts payable for vendor invoices', 4700.00, 4700.00, 'Draft', 0, GETUTCDATE()),
        (NEWID(), @TenantId, 'JE-2024-0004', CAST(GETUTCDATE() AS DATE),                      'Cash receipt from customer payment', 12500.00, 12500.00, 'Draft', 0, GETUTCDATE());

    PRINT '  ✓ Journal Entries seeded (4 entries)';
END
ELSE
    PRINT '  ℹ Journal Entries already exist, skipping...';
GO

-- ============================================================================
-- 7. Seed Bank Reconciliations
-- ============================================================================
PRINT 'Seeding Bank Reconciliations...';

IF NOT EXISTS (SELECT 1 FROM Finance.BankReconciliation WHERE TenantId = @TenantId AND BankAccountNumber = '12345679')
BEGIN
    INSERT INTO Finance.BankReconciliation (BankReconciliationId, TenantId, BankAccountNumber, BankName, BankStatementDate, BankBalance, BookBalance, OutstandingDeposits, OutstandingChecks, Discrepancy, StatusCode, IsDeleted, CreatedDateUtc)
    VALUES 
        (NEWID(), @TenantId, '12345679', 'First National Bank - Operating', CAST(DATEADD(DAY, -3, GETUTCDATE()) AS DATE), 125000.00, 124300.00, 2500.00, 3200.00, 0.00, 'Reconciled', 0, GETUTCDATE()),
        (NEWID(), @TenantId, '98765432', 'First National Bank - Payroll',  CAST(DATEADD(DAY, -1, GETUTCDATE()) AS DATE), 50000.00,  50000.00, 0.00,     0.00,     0.00, 'Reconciled', 0, GETUTCDATE());

    PRINT '  ✓ Bank Reconciliations seeded (2 accounts)';
END
ELSE
    PRINT '  ℹ Bank Reconciliations already exist, skipping...';
GO

PRINT '======================================================================';
PRINT '✓ Finance seed data population completed successfully!';
PRINT '======================================================================';
PRINT '';
PRINT 'Summary of seeded data:';
PRINT '  - GL Accounts: 24';
PRINT '  - Vendors: 8';
PRINT '  - AP Invoices: 6';
PRINT '  - AP Payments: 3';
PRINT '  - Accounting Periods: 6';
PRINT '  - Journal Entries: 4';
PRINT '  - Bank Reconciliations: 2';