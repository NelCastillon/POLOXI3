# Finance Workflow API Contract

## Overview
This document defines the API contracts that need to be implemented in the `ApiClient` service to support the Finance module workflows.

## Base Configuration
- **Tenant ID**: `Guid.Parse("00000000-0000-0000-0000-000000000001")` (default for all calls)
- **Base Path**: `/api/finance/`
- **Response Format**: Paginated results with `Items` collection

## API Method Contracts

### 1. GL Accounts
**Endpoint**: `GET /api/finance/gl-accounts/search`

```csharp
public async Task<PagedResult<GLAccountDto>> SearchGLAccountsAsync(Guid tenantId, string? searchTerm)
```

**GLAccountDto Properties**:
- `string AccountCode` - Account code (e.g., "1000")
- `string AccountName` - Account name (e.g., "Cash")
- `string AccountTypeCode` - Account type (e.g., "ASSET", "LIABILITY")
- `bool IsActive` - Active status

---

### 2. Journal Entries
**Endpoint**: `GET /api/finance/journal-entries/search`

```csharp
public async Task<PagedResult<JournalEntryDto>> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm)
```

**JournalEntryDto Properties**:
- `string JournalNumber` - Unique journal entry number
- `DateTime EntryDate` - Date of entry
- `string Description` - Entry description
- `string StatusCode` - Status (e.g., "DRAFT", "POSTED")
- `DateTime? PostedDateUtc` - Date posted to GL

---

### 3. Journal Entry Lines
**Endpoint**: `GET /api/finance/journal-entry-lines/search`

```csharp
public async Task<PagedResult<JournalEntryLineDto>> SearchJournalEntryLinesAsync(Guid tenantId, string? searchTerm)
```

**JournalEntryLineDto Properties**:
- `int LineNumber` - Line sequence
- `string JournalNumber` - Parent journal entry
- `string AccountCode` - GL account code
- `string Description` - Line description
- `decimal DebitAmount` - Debit amount
- `decimal CreditAmount` - Credit amount

---

### 4. Bank Reconciliation
**Endpoint**: `GET /api/finance/bank-reconciliation/search`

```csharp
public async Task<PagedResult<BankReconciliationDto>> SearchBankReconciliationsAsync(Guid tenantId, string? searchTerm)
```

**BankReconciliationDto Properties**:
- `string BankAccountCode` - Bank account code
- `DateTime ReconciliationDate` - Reconciliation date
- `decimal BankBalance` - Balance per bank statement
- `decimal BookBalance` - Balance per GL
- `decimal Variance` - Difference (BankBalance - BookBalance)
- `string StatusCode` - Status (e.g., "IN_PROGRESS", "COMPLETE")

---

### 5. Vendors
**Endpoint**: `GET /api/finance/vendors/search`

```csharp
public async Task<PagedResult<VendorDto>> SearchVendorsAsync(Guid tenantId, string? searchTerm)
```

**VendorDto Properties**:
- `string VendorCode` - Unique vendor code
- `string VendorName` - Vendor name
- `string VendorType` - Vendor type (e.g., "VENDOR", "SUPPLIER")
- `string ContactName` - Primary contact
- `bool IsActive` - Active status

---

### 6. AP Invoices
**Endpoint**: `GET /api/finance/ap-invoices/search`

```csharp
public async Task<PagedResult<APInvoiceDto>> SearchAPInvoicesAsync(Guid tenantId, string? searchTerm)
```

**APInvoiceDto Properties**:
- `string InvoiceNumber` - Vendor invoice number
- `string VendorCode` - Vendor code
- `DateTime InvoiceDate` - Vendor invoice date
- `DateTime DueDate` - Payment due date
- `decimal InvoiceAmount` - Invoice total
- `string StatusCode` - Status (e.g., "NEW", "APPROVED", "PAID")

---

### 7. AP Invoice Lines
**Endpoint**: `GET /api/finance/ap-invoice-lines/search`

```csharp
public async Task<PagedResult<APInvoiceLineDto>> SearchAPInvoiceLinesAsync(Guid tenantId, string? searchTerm)
```

**APInvoiceLineDto Properties**:
- `int LineNumber` - Line sequence
- `string InvoiceNumber` - Parent invoice
- `string AccountCode` - GL account for expense
- `string Description` - Line description
- `decimal LineAmount` - Line total
- `decimal Quantity` - Quantity received

---

### 8. AP Payments
**Endpoint**: `GET /api/finance/ap-payments/search`

```csharp
public async Task<PagedResult<APPaymentDto>> SearchAPPaymentsAsync(Guid tenantId, string? searchTerm)
```

**APPaymentDto Properties**:
- `string PaymentNumber` - Unique payment number
- `string InvoiceNumber` - Related invoice
- `string VendorCode` - Vendor code
- `DateTime PaymentDate` - Payment date
- `decimal PaymentAmount` - Amount paid
- `string StatusCode` - Status (e.g., "SCHEDULED", "PROCESSED")

---

### 9. Accounting Periods
**Endpoint**: `GET /api/finance/accounting-periods/search`

```csharp
public async Task<PagedResult<AccountingPeriodDto>> SearchAccountingPeriodsAsync(Guid tenantId, string? searchTerm)
```

**AccountingPeriodDto Properties**:
- `string PeriodName` - Period name (e.g., "January 2026")
- `int FiscalYear` - Fiscal year
- `int PeriodNumber` - Period number (1-12)
- `DateTime StartDate` - Period start date
- `DateTime EndDate` - Period end date
- `string StatusCode` - Status (e.g., "OPEN", "CLOSED")

---

### 10. Period Close
**Endpoint**: `GET /api/finance/period-close/search`

```csharp
public async Task<PagedResult<PeriodCloseDto>> SearchPeriodCloseAsync(Guid tenantId, string? searchTerm)
```

**PeriodCloseDto Properties**:
- `string PeriodName` - Period closed
- `int FiscalYear` - Fiscal year
- `DateTime ClosedDate` - Close date
- `string ClosedBy` - User who closed
- `string StatusCode` - Status (e.g., "OPEN", "CLOSED")

---

### 11. Deferred Revenue
**Endpoint**: `GET /api/finance/deferred-revenue/search`

```csharp
public async Task<PagedResult<DeferredRevenueDto>> SearchDeferredRevenueAsync(Guid tenantId, string? searchTerm)
```

**DeferredRevenueDto Properties**:
- `string ContractNumber` - Contract reference
- `string CustomerCode` - Customer code
- `decimal TotalAmount` - Total contract value
- `decimal RecognizedAmount` - Already recognized
- `decimal DeferredAmount` - Still deferred
- `string StatusCode` - Status (e.g., "ACTIVE", "COMPLETE")

---

### 12. Deferred Revenue Recognition
**Endpoint**: `GET /api/finance/deferred-revenue-recognition/search`

```csharp
public async Task<PagedResult<DeferredRevenueRecognitionDto>> SearchDeferredRevenueRecognitionAsync(Guid tenantId, string? searchTerm)
```

**DeferredRevenueRecognitionDto Properties**:
- `string RecognitionNumber` - Recognition transaction number
- `string ContractNumber` - Contract reference
- `DateTime RecognitionDate` - Recognition date
- `decimal RecognitionAmount` - Amount recognized
- `string StatusCode` - Status (e.g., "DRAFT", "POSTED")

---

### 13. Bad Debt
**Endpoint**: `GET /api/finance/bad-debt/search`

```csharp
public async Task<PagedResult<BadDebtDto>> SearchBadDebtAsync(Guid tenantId, string? searchTerm)
```

**BadDebtDto Properties**:
- `string BadDebtNumber` - Bad debt transaction number
- `string InvoiceNumber` - Related invoice
- `string CustomerCode` - Customer code
- `decimal WriteOffAmount` - Amount written off
- `DateTime WriteOffDate` - Write-off date
- `string StatusCode` - Status (e.g., "REQUESTED", "APPROVED")

---

### 14. Cash Receipts
**Endpoint**: `GET /api/finance/cash-receipts/search`

```csharp
public async Task<PagedResult<CashReceiptDto>> SearchCashReceiptsAsync(Guid tenantId, string? searchTerm)
```

**CashReceiptDto Properties**:
- `string ReceiptNumber` - Receipt number
- `string InvoiceNumber` - Related invoice
- `string CustomerCode` - Customer code
- `DateTime ReceiptDate` - Receipt date
- `decimal ReceiptAmount` - Amount received
- `string StatusCode` - Status (e.g., "PENDING", "DEPOSITED")

---

### 15. Trial Balance
**Endpoint**: `GET /api/finance/trial-balance/search`

```csharp
public async Task<PagedResult<TrialBalanceDto>> SearchTrialBalanceAsync(Guid tenantId, string? searchTerm)
```

**TrialBalanceDto Properties**:
- `string AccountCode` - GL account code
- `string AccountName` - GL account name
- `decimal DebitBalance` - Debit balance
- `decimal CreditBalance` - Credit balance
- `DateTime AsOfDate` - Date as of

---

## Common Response Structure

All endpoints return:

```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
```

## Error Handling

Expected HTTP Status Codes:
- `200 OK` - Successful search
- `400 Bad Request` - Invalid search parameters
- `401 Unauthorized` - Missing authentication
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Tenant not found
- `500 Internal Server Error` - Server error

## Search Parameters

All search methods accept:
- **tenantId** (required): Tenant GUID
- **searchTerm** (optional): Free-text search across all displayable fields

## Implementation Examples

### Using ApiClient in Blazor Component

```csharp
@code {
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private string? _searchTerm;
    private List<GLAccountDto>? _items;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var result = await Api.SearchGLAccountsAsync(_tenantId, _searchTerm);
            _items = result?.Items.ToList() ?? [];
        }
        catch (HttpRequestException ex)
        {
            // Handle API errors
            Console.WriteLine($"Error loading GL Accounts: {ex.Message}");
        }
    }
}
```

## DTOs Location

All DTO classes should be defined in:
```
src/Ams.Application/Dto/Finance/
├── GLAccountDto.cs
├── JournalEntryDto.cs
├── JournalEntryLineDto.cs
├── BankReconciliationDto.cs
├── VendorDto.cs
├── APInvoiceDto.cs
├── APInvoiceLineDto.cs
├── APPaymentDto.cs
├── AccountingPeriodDto.cs
├── PeriodCloseDto.cs
├── DeferredRevenueDto.cs
├── DeferredRevenueRecognitionDto.cs
├── BadDebtDto.cs
├── CashReceiptDto.cs
└── TrialBalanceDto.cs
```

## Next Steps

1. Create DTO classes in `src/Ams.Application/Dto/Finance/`
2. Implement API endpoints in backend
3. Add search methods to `ApiClient` service
4. Test each workflow page with real data
5. Add additional features (CRUD, reporting, etc.)
