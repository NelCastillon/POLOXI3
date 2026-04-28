using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IFinanceService
{
    // ── GL Accounts ──────────────────────────────────────────────
    Task<GLAccountDto?> GetGLAccountByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<GLAccountDto>> SearchGLAccountsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── Journal Entries ──────────────────────────────────────────
    Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryDto>> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── Vendors ──────────────────────────────────────────────────
    Task<VendorDto?> GetVendorByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<VendorDto>> SearchVendorsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── AP Invoices ──────────────────────────────────────────────
    Task<ApInvoiceDto?> GetApInvoiceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ApInvoiceDto>> SearchApInvoicesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── AP Payments ──────────────────────────────────────────────
    Task<ApPaymentDto?> GetApPaymentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ApPaymentDto>> SearchApPaymentsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── Accounting Periods ───────────────────────────────────────
    Task<AccountingPeriodDto?> GetAccountingPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountingPeriodDto>> SearchAccountingPeriodsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);

    // ── Bank Reconciliation ──────────────────────────────────────
    Task<BankReconciliationDto?> GetBankReconciliationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BankReconciliationDto>> SearchBankReconciliationsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
