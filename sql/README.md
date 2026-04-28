# Finance Schema and Seed Data SQL Scripts

## Overview

This directory contains SQL scripts for creating and populating the Finance schema for the AMS (Account Management System) application. These scripts implement an enterprise-grade financial data management system supporting accounts payable, general ledger, banking, and accounting period management.

## Files

### 1. `01_Finance_Schema.sql`
Creates the complete Finance schema with all necessary tables and indexes.

**Tables Created:**
- **Finance.GLAccount** - General Ledger Chart of Accounts
  - Supports hierarchical account structure with parent accounts
  - Includes account types (Asset, Liability, Equity, Revenue, Expense)
  - 24-account standard chart of accounts template

- **Finance.Vendor** - Vendor Master Data
  - Vendor profile and contact information
  - Payment terms and currency preferences
  - Tax ID and vendor classification

- **Finance.ApInvoice** - Accounts Payable Invoices
  - Invoice tracking from receipt through payment
  - Multiple status states (Open, Paid, PartiallyPaid)
  - GL account and agreement linkage

- **Finance.ApPayment** - Payment Management
  - Payment method tracking (ACH, Check, Wire, etc.)
  - Invoice payment matching
  - Payment status workflow

- **Finance.AccountingPeriod** - Period Management
  - Monthly and quarterly period definitions
  - Fiscal year tracking
  - Period status (Open, Closed, Locked)

- **Finance.JournalEntry** - Journal Posting
  - Debit/credit entry tracking
  - Status workflow (Draft, Posted, Reversed)
  - Multi-tenant period tracking

- **Finance.BankReconciliation** - Bank Reconciliation
  - Bank statement reconciliation tracking
  - Outstanding deposits and checks
  - Reconciliation discrepancy tracking

### 2. `02_Finance_SeedData.sql`
Populates the Finance schema with realistic enterprise-level test data.

**Seed Data Includes:**
- 24 GL Accounts covering complete chart of accounts
- 8 Vendors with realistic business data
- 6 AP Invoices in various states (Paid, Open, PartiallyPaid)
- 3 Payments with different payment methods
- 6 Accounting Periods (January-April 2024 + Q1 + FY2024)
- 4 Journal Entries demonstrating typical transactions
- 2 Bank Reconciliations for checking and payroll accounts

## Prerequisites

- SQL Server 2016 or later (or Azure SQL Database)
- Appropriate database permissions to create schemas and tables
- The Core, Client, and IAM schemas should already exist

## Installation Instructions

### Step 1: Create the Schema
```sql
-- Open SQL Server Management Studio or your preferred SQL client
-- Connect to your AMS database
-- Open and execute: 01_Finance_Schema.sql
```

### Step 2: Populate with Seed Data
```sql
-- After schema creation is successful
-- Open and execute: 02_Finance_SeedData.sql
```

### Step 3: Verify Installation
```sql
-- Verify tables were created
SELECT TABLE_SCHEMA, TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'Finance'
ORDER BY TABLE_NAME;

-- Verify seed data
SELECT COUNT(*) AS GLAccountCount FROM Finance.GLAccount;
SELECT COUNT(*) AS VendorCount FROM Finance.Vendor;
SELECT COUNT(*) AS InvoiceCount FROM Finance.ApInvoice;
SELECT COUNT(*) AS PaymentCount FROM Finance.ApPayment;
```

## Data Details

### GL Accounts Structure
The seed data includes a standard 24-account chart of accounts organized as follows:

**Assets (1000-1510)**
- 1000: Cash - Operating Account
- 1010: Cash - Payroll Account
- 1020: Cash - Petty Cash
- 1200: Accounts Receivable
- 1210: Allowance for Doubtful Accounts
- 1500: Fixed Assets - Equipment
- 1510: Accumulated Depreciation

**Liabilities (2000-2200)**
- 2000: Accounts Payable
- 2100: Payroll Liabilities
- 2200: Deferred Revenue

**Equity (3000-3100)**
- 3000: Common Stock
- 3100: Retained Earnings

**Revenue (4000-4100)**
- 4000: Service Revenue
- 4100: Consulting Revenue

**Expenses (5000-5900)**
- 5000: Salaries and Wages
- 5100: Employee Benefits
- 5200: Rent Expense
- 5300: Utilities Expense
- 5400: Depreciation Expense
- 5500: Office Supplies
- 5600: Marketing and Advertising
- 5700: Travel and Meals
- 5800: Professional Services
- 5900: Bad Debt Expense

### Vendor Sample Data
The 8 vendors represent different types of business relationships:

1. **Office Supplies Inc.** - Supplier (Net 30)
2. **Global Tech Solutions** - Technology (Net 60)
3. **Premium Property Management** - Landlord (Net 15)
4. **Professional Staffing Group** - Contractor (Net 45)
5. **Corporate Legal Associates** - Professional Services (Net 30)
6. **Energy Systems LLC** - Utility (Net 30)
7. **Premium Benefits Broker** - Insurance (Due 15)
8. **Cloud Infrastructure Partners** - Technology (Net 45)

### Sample Invoices
Sample invoices demonstrate various scenarios:
- Paid invoices (2024-0001): Fully paid office supplies
- Open invoices (2024-0002, 0003, 0005, 0006): Various vendors and amounts
- Partially paid invoices (2024-0004): Multi-payment scenario

### Demo Tenant
All seed data is associated with the demo tenant:
- **TenantId**: `00000000-0000-0000-0000-000000000001`
- **Created By**: `00000000-0000-0000-0000-000000000002` (demo admin user)

## Performance Considerations

### Indexes
Both scripts include optimal indexes for common queries:
- TenantId filtering
- Status code filtering
- Date range queries
- Foreign key relationships

### Scalability
- Tables support GUID primary keys for distributed systems
- Soft delete pattern via IsDeleted flag
- CreatedDateUtc and ModifiedDateUtc for audit trails
- Support for multi-tenant operations

## Security Best Practices

1. **Row-Level Security**: Implement RLS policies to restrict data by tenant
2. **Audit Logging**: Use triggers to track all DML operations
3. **Column Encryption**: Consider encrypting sensitive data (Tax IDs, account numbers)
4. **Permissions**: Grant appropriate role-based access

## Customization

### Adding More Vendors
```sql
INSERT INTO Finance.Vendor 
    (VendorId, TenantId, VendorCode, VendorName, ContactName, Email, Phone, 
     PaymentTermsCode, CurrencyCode, TaxId, VendorTypeCode, StatusCode, IsDeleted, CreatedDateUtc)
VALUES 
    (NEWID(), '00000000-0000-0000-0000-000000000001', 'VEN-009', 'New Vendor', 
     'Contact Name', 'contact@vendor.com', '+1 555 0000', 'Net30', 'USD', 
     '12-3456789', 'Supplier', 'Active', 0, GETUTCDATE());
```

### Adding More GL Accounts
```sql
INSERT INTO Finance.GLAccount
    (GLAccountId, TenantId, AccountCode, AccountName, AccountTypeCode, Description, IsActive, IsDeleted, CreatedDateUtc)
VALUES
    (NEWID(), '00000000-0000-0000-0000-000000000001', '1030', 'Cash - Savings', 'Asset', 'Savings account', 1, 0, GETUTCDATE());
```

## Maintenance

### Regular Tasks
1. **Archive Old Periods**: Move closed periods to archive as needed
2. **Reconciliation Reviews**: Ensure bank reconciliations are completed monthly
3. **Invoice Aging**: Monitor open invoices for timely payment
4. **Data Validation**: Run integrity checks for referential integrity

### Cleanup (if needed)
```sql
-- CAUTION: Only run in development environments
-- DELETE FROM Finance.ApPayment WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- DELETE FROM Finance.ApInvoice WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- DELETE FROM Finance.Vendor WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- DELETE FROM Finance.JournalEntry WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- DELETE FROM Finance.AccountingPeriod WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- DELETE FROM Finance.GLAccount WHERE TenantId = '00000000-0000-0000-0000-000000000001';
```

## Related Documentation

- Application Layers: See `/src/Ams.Application/` for DTOs and service interfaces
- Repositories: See `/src/Ams.Infrastructure/Persistence/Repositories/` for data access patterns
- API Endpoints: See `/src/Ams.Api/Controllers/FinanceController.cs` for API routes
- Database Migrations: See `DatabaseMigrator.cs` for automatic migration runner

## Support

For issues or questions:
1. Check the main AMS project documentation
2. Review the Finance service implementation in the application layer
3. Run the verification queries to diagnose data issues

## License

These scripts are part of the AMS (Account Management System) project.
