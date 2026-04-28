using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FinanceController : ControllerBase
{
    private readonly IFinanceService _service;
    public FinanceController(IFinanceService service) => _service = service;

    // ── GL Accounts ──────────────────────────────────────────────
    [HttpGet("glaccounts/{id:guid}")]
    public async Task<IActionResult> GetGLAccountById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetGLAccountByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("glaccounts")]
    public async Task<IActionResult> SearchGLAccounts([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchGLAccountsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    // ── Journal Entries ──────────────────────────────────────────
    [HttpGet("journalentries/{id:guid}")]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetJournalEntryByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("journalentries")]
    public async Task<IActionResult> SearchJournalEntries([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchJournalEntriesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    // ── AP Invoices ──────────────────────────────────────────────
    [HttpGet("ap-invoices/{id:guid}")]
    public async Task<IActionResult> GetApInvoiceById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetApInvoiceByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("ap-invoices")]
    public async Task<IActionResult> SearchApInvoices([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchApInvoicesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    // ── AP Payments ──────────────────────────────────────────────
    [HttpGet("ap-payments/{id:guid}")]
    public async Task<IActionResult> GetApPaymentById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetApPaymentByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("ap-payments")]
    public async Task<IActionResult> SearchApPayments([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchApPaymentsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    // ── Accounting Periods ───────────────────────────────────────
    [HttpGet("accounting-periods/{id:guid}")]
    public async Task<IActionResult> GetAccountingPeriodById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetAccountingPeriodByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("accounting-periods")]
    public async Task<IActionResult> SearchAccountingPeriods([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAccountingPeriodsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    // ── Bank Reconciliation ──────────────────────────────────────
    [HttpGet("bank-reconciliation/{id:guid}")]
    public async Task<IActionResult> GetBankReconciliationById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetBankReconciliationByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("bank-reconciliation")]
    public async Task<IActionResult> SearchBankReconciliations([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchBankReconciliationsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
