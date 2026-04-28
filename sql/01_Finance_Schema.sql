-- ============================================================================
-- Finance Schema Creation Script
-- Description: Creates the Finance schema and all related tables for
--              accounts payable, general ledger, banking, and accounting
--              period management
-- ============================================================================

-- Create Finance schema if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Finance')
BEGIN
    EXEC sp_executesql N'CREATE SCHEMA Finance AUTHORIZATION dbo';
    PRINT 'Finance schema created successfully.';
END
ELSE
    PRINT 'Finance schema already exists.';
GO

-- ============================================================================
-- Finance.GLAccount - General Ledger Accounts
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GLAccount' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.GLAccount (
        GLAccountId          UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        AccountCode          NVARCHAR(50)      NOT NULL,
        AccountName          NVARCHAR(200)     NOT NULL,
        AccountTypeCode      NVARCHAR(50)      NOT NULL DEFAULT 'Asset',
        Description          NVARCHAR(500)     NULL,
        ParentGLAccountId    UNIQUEIDENTIFIER  NULL,
        IsActive             BIT               NOT NULL DEFAULT 1,
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL,

        -- Constraints
        CONSTRAINT FK_GLAccount_Parent FOREIGN KEY (ParentGLAccountId) 
            REFERENCES Finance.GLAccount(GLAccountId)
    );

    CREATE NONCLUSTERED INDEX IX_GLAccount_TenantId_Code 
        ON Finance.GLAccount(TenantId, AccountCode);
    CREATE NONCLUSTERED INDEX IX_GLAccount_TenantId_Type 
        ON Finance.GLAccount(TenantId, AccountTypeCode);

    PRINT 'Finance.GLAccount table created successfully.';
END
ELSE
    PRINT 'Finance.GLAccount table already exists.';
GO

-- ============================================================================
-- Finance.Vendor - Vendor Master
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vendor' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.Vendor (
        VendorId             UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        VendorCode           NVARCHAR(50)      NOT NULL,
        VendorName           NVARCHAR(200)     NOT NULL,
        ContactName          NVARCHAR(200)     NULL,
        Email                NVARCHAR(200)     NULL,
        Phone                NVARCHAR(20)      NULL,
        PaymentTermsCode     NVARCHAR(50)      NOT NULL DEFAULT 'Net30',
        CurrencyCode         NVARCHAR(3)       NOT NULL DEFAULT 'USD',
        TaxId                NVARCHAR(50)      NULL,
        VendorTypeCode       NVARCHAR(50)      NOT NULL DEFAULT 'Supplier',
        StatusCode           NVARCHAR(50)      NOT NULL DEFAULT 'Active',
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL
    );

    CREATE NONCLUSTERED INDEX IX_Vendor_TenantId_Code 
        ON Finance.Vendor(TenantId, VendorCode);
    CREATE NONCLUSTERED INDEX IX_Vendor_TenantId_Status 
        ON Finance.Vendor(TenantId, StatusCode);

    PRINT 'Finance.Vendor table created successfully.';
END
ELSE
    PRINT 'Finance.Vendor table already exists.';
GO

-- ============================================================================
-- Finance.ApInvoice - Accounts Payable Invoices
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ApInvoice' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.ApInvoice (
        ApInvoiceId          UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        VendorId             UNIQUEIDENTIFIER  NOT NULL,
        InvoiceNumber        NVARCHAR(50)      NOT NULL,
        InvoiceDate          DATE              NOT NULL,
        DueDate              DATE              NOT NULL,
        Amount               DECIMAL(18,2)     NOT NULL,
        AmountPaid           DECIMAL(18,2)     NOT NULL DEFAULT 0,
        TaxAmount            DECIMAL(18,2)     NOT NULL DEFAULT 0,
        Description          NVARCHAR(500)     NULL,
        StatusCode           NVARCHAR(50)      NOT NULL DEFAULT 'Open',
        GLAccountId          UNIQUEIDENTIFIER  NULL,
        AgreementId          UNIQUEIDENTIFIER  NULL,
        Notes                NVARCHAR(MAX)     NULL,
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL,

        -- Constraints
        CONSTRAINT FK_ApInvoice_Vendor FOREIGN KEY (VendorId) 
            REFERENCES Finance.Vendor(VendorId),
        CONSTRAINT FK_ApInvoice_GLAccount FOREIGN KEY (GLAccountId) 
            REFERENCES Finance.GLAccount(GLAccountId)
    );

    CREATE NONCLUSTERED INDEX IX_ApInvoice_TenantId_Status 
        ON Finance.ApInvoice(TenantId, StatusCode);
    CREATE NONCLUSTERED INDEX IX_ApInvoice_VendorId 
        ON Finance.ApInvoice(VendorId);
    CREATE NONCLUSTERED INDEX IX_ApInvoice_DueDate 
        ON Finance.ApInvoice(DueDate);

    PRINT 'Finance.ApInvoice table created successfully.';
END
ELSE
    PRINT 'Finance.ApInvoice table already exists.';
GO

-- ============================================================================
-- Finance.ApPayment - Accounts Payable Payments
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ApPayment' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.ApPayment (
        ApPaymentId          UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        VendorId             UNIQUEIDENTIFIER  NOT NULL,
        ApInvoiceId          UNIQUEIDENTIFIER  NULL,
        PaymentDate          DATE              NOT NULL,
        Amount               DECIMAL(18,2)     NOT NULL,
        PaymentMethodCode    NVARCHAR(50)      NOT NULL DEFAULT 'ACH',
        ReferenceNumber      NVARCHAR(100)     NULL,
        Notes                NVARCHAR(MAX)     NULL,
        StatusCode           NVARCHAR(50)      NOT NULL DEFAULT 'Pending',
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL,

        -- Constraints
        CONSTRAINT FK_ApPayment_Vendor FOREIGN KEY (VendorId) 
            REFERENCES Finance.Vendor(VendorId),
        CONSTRAINT FK_ApPayment_ApInvoice FOREIGN KEY (ApInvoiceId) 
            REFERENCES Finance.ApInvoice(ApInvoiceId)
    );

    CREATE NONCLUSTERED INDEX IX_ApPayment_TenantId_Status 
        ON Finance.ApPayment(TenantId, StatusCode);
    CREATE NONCLUSTERED INDEX IX_ApPayment_PaymentDate 
        ON Finance.ApPayment(PaymentDate);
    CREATE NONCLUSTERED INDEX IX_ApPayment_VendorId 
        ON Finance.ApPayment(VendorId);

    PRINT 'Finance.ApPayment table created successfully.';
END
ELSE
    PRINT 'Finance.ApPayment table already exists.';
GO

-- ============================================================================
-- Finance.AccountingPeriod - Accounting Periods
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountingPeriod' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.AccountingPeriod (
        AccountingPeriodId   UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        PeriodCode           NVARCHAR(50)      NOT NULL,
        PeriodName           NVARCHAR(200)     NOT NULL,
        StartDate            DATE              NOT NULL,
        EndDate              DATE              NOT NULL,
        StatusCode           NVARCHAR(50)      NOT NULL DEFAULT 'Open',
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL
    );

    CREATE NONCLUSTERED INDEX IX_AccountingPeriod_TenantId_Status 
        ON Finance.AccountingPeriod(TenantId, StatusCode);
    CREATE NONCLUSTERED INDEX IX_AccountingPeriod_DateRange 
        ON Finance.AccountingPeriod(StartDate, EndDate);

    PRINT 'Finance.AccountingPeriod table created successfully.';
END
ELSE
    PRINT 'Finance.AccountingPeriod table already exists.';
GO

-- ============================================================================
-- Finance.JournalEntry - Journal Entries
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JournalEntry' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.JournalEntry (
        JournalEntryId       UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        EntryNumber          NVARCHAR(50)      NOT NULL,
        EntryDate            DATE              NOT NULL,
        Description          NVARCHAR(500)     NULL,
        TotalDebit           DECIMAL(18,2)     NOT NULL DEFAULT 0,
        TotalCredit          DECIMAL(18,2)     NOT NULL DEFAULT 0,
        StatusCode           NVARCHAR(50)      NOT NULL DEFAULT 'Draft',
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL
    );

    CREATE NONCLUSTERED INDEX IX_JournalEntry_TenantId_Status 
        ON Finance.JournalEntry(TenantId, StatusCode);
    CREATE NONCLUSTERED INDEX IX_JournalEntry_EntryDate 
        ON Finance.JournalEntry(EntryDate);

    PRINT 'Finance.JournalEntry table created successfully.';
END
ELSE
    PRINT 'Finance.JournalEntry table already exists.';
GO

-- ============================================================================
-- Finance.BankReconciliation - Bank Reconciliation
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BankReconciliation' AND schema_id = SCHEMA_ID('Finance'))
BEGIN
    CREATE TABLE Finance.BankReconciliation (
        BankReconciliationId UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        TenantId             UNIQUEIDENTIFIER  NOT NULL,
        BankAccountNumber    NVARCHAR(50)      NOT NULL,
        BankName             NVARCHAR(200)     NOT NULL,
        BankStatementDate    DATE              NOT NULL,
        BankBalance          DECIMAL(18,2)     NOT NULL DEFAULT 0,
        BookBalance          DECIMAL(18,2)     NOT NULL DEFAULT 0,
        OutstandingDeposits  DECIMAL(18,2)     NOT NULL DEFAULT 0,
        OutstandingChecks    DECIMAL(18,2)     NOT NULL DEFAULT 0,
        Discrepancy          DECIMAL(18,2)     NOT NULL DEFAULT 0,
        StatusCode           NVARCHAR(50)      NOT NULL DEFAULT 'Pending',
        IsDeleted            BIT               NOT NULL DEFAULT 0,
        CreatedDateUtc       DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc      DATETIME2(7)      NULL
    );

    CREATE NONCLUSTERED INDEX IX_BankReconciliation_TenantId_Status 
        ON Finance.BankReconciliation(TenantId, StatusCode);
    CREATE NONCLUSTERED INDEX IX_BankReconciliation_StatementDate 
        ON Finance.BankReconciliation(BankStatementDate);

    PRINT 'Finance.BankReconciliation table created successfully.';
END
ELSE
    PRINT 'Finance.BankReconciliation table already exists.';
GO

PRINT '======================================================================';
PRINT 'Finance schema and all tables created successfully!';
PRINT '======================================================================';
