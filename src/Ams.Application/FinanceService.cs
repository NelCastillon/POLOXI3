using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application;

public sealed class FinanceService : IFinanceService
{
    private readonly IGLAccountRepository _glRepo;
    private readonly IJournalEntryRepository _jeRepo;
    private readonly IVendorRepository _vendorRepo;
    private readonly IApInvoiceRepository _apInvoiceRepo;
    private readonly IApPaymentRepository _apPaymentRepo;
    private readonly IAccountingPeriodRepository _accountingPeriodRepo;
    private readonly IBankReconciliationRepository _bankReconRepo;

    public FinanceService(
        IGLAccountRepository glRepo,
        IJournalEntryRepository jeRepo,
        IVendorRepository vendorRepo,
        IApInvoiceRepository apInvoiceRepo,
        IApPaymentRepository apPaymentRepo,
        IAccountingPeriodRepository accountingPeriodRepo,
        IBankReconciliationRepository bankReconRepo)
    {
        _glRepo = glRepo;
        _jeRepo = jeRepo;
        _vendorRepo = vendorRepo;
        _apInvoiceRepo = apInvoiceRepo;
        _apPaymentRepo = apPaymentRepo;
        _accountingPeriodRepo = accountingPeriodRepo;
        _bankReconRepo = bankReconRepo;
    }

    // ── GL Accounts ──────────────────────────────────────────────
    public Task<GLAccountDto?> GetGLAccountByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _glRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<GLAccountDto>> SearchGLAccountsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _glRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateGLAccountAsync(CreateGLAccountRequest request, CancellationToken cancellationToken = default)
        => _glRepo.CreateAsync(request, cancellationToken);

    public Task UpdateGLAccountAsync(Guid id, UpdateGLAccountRequest request, CancellationToken cancellationToken = default)
        => _glRepo.UpdateAsync(id, request, cancellationToken);

    // ── Journal Entries ──────────────────────────────────────────
    public Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _jeRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<JournalEntryDto>> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _jeRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateJournalEntryAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken = default)
        => _jeRepo.CreateAsync(request, cancellationToken);

    public Task UpdateJournalEntryAsync(Guid id, UpdateJournalEntryRequest request, CancellationToken cancellationToken = default)
        => _jeRepo.UpdateAsync(id, request, cancellationToken);

    // ── Vendors ──────────────────────────────────────────────────
    public Task<VendorDto?> GetVendorByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _vendorRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<VendorDto>> SearchVendorsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _vendorRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateVendorAsync(CreateVendorRequest request, CancellationToken cancellationToken = default)
        => _vendorRepo.CreateAsync(request, cancellationToken);

    public Task UpdateVendorAsync(Guid id, UpdateVendorRequest request, CancellationToken cancellationToken = default)
        => _vendorRepo.UpdateAsync(id, request, cancellationToken);

    // ── AP Invoices ──────────────────────────────────────────────
    public Task<ApInvoiceDto?> GetApInvoiceByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _apInvoiceRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<ApInvoiceDto>> SearchApInvoicesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _apInvoiceRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    // ── AP Payments ──────────────────────────────────────────────
    public Task<ApPaymentDto?> GetApPaymentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _apPaymentRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<ApPaymentDto>> SearchApPaymentsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _apPaymentRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    // ── Accounting Periods ───────────────────────────────────────
    public Task<AccountingPeriodDto?> GetAccountingPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _accountingPeriodRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AccountingPeriodDto>> SearchAccountingPeriodsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _accountingPeriodRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAccountingPeriodAsync(CreateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
        => _accountingPeriodRepo.CreateAsync(request, cancellationToken);

    public Task UpdateAccountingPeriodAsync(Guid id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
        => _accountingPeriodRepo.UpdateAsync(id, request, cancellationToken);

    // ── Bank Reconciliation ──────────────────────────────────────
    public Task<BankReconciliationDto?> GetBankReconciliationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _bankReconRepo.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<BankReconciliationDto>> SearchBankReconciliationsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _bankReconRepo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateBankReconciliationAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken = default)
        => _bankReconRepo.CreateAsync(request, cancellationToken);

    public Task UpdateBankReconciliationAsync(Guid id, UpdateBankReconciliationRequest request, CancellationToken cancellationToken = default)
        => _bankReconRepo.UpdateAsync(id, request, cancellationToken);
}
