using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Finance;
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

    [HttpPost("glaccounts")]
    public async Task<IActionResult> CreateGLAccount([FromBody] CreateGLAccountRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateGLAccountAsync(request, cancellationToken));

    [HttpPut("glaccounts/{id:guid}")]
    public async Task<IActionResult> UpdateGLAccount(Guid id, [FromBody] UpdateGLAccountRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateGLAccountAsync(id, request, cancellationToken);
        return NoContent();
    }

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

    [HttpPost("journalentries")]
    public async Task<IActionResult> CreateJournalEntry([FromBody] CreateJournalEntryRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateJournalEntryAsync(request, cancellationToken));

    [HttpPut("journalentries/{id:guid}")]
    public async Task<IActionResult> UpdateJournalEntry(Guid id, [FromBody] UpdateJournalEntryRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateJournalEntryAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("accounting-periods")]
    public async Task<IActionResult> CreateAccountingPeriod([FromBody] CreateAccountingPeriodRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAccountingPeriodAsync(request, cancellationToken));

    [HttpPut("accounting-periods/{id:guid}")]
    public async Task<IActionResult> UpdateAccountingPeriod(Guid id, [FromBody] UpdateAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAccountingPeriodAsync(id, request, cancellationToken);
        return NoContent();
    }

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

    [HttpPost("bank-reconciliation")]
    public async Task<IActionResult> CreateBankReconciliation([FromBody] CreateBankReconciliationRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateBankReconciliationAsync(request, cancellationToken));

    [HttpPut("bank-reconciliation/{id:guid}")]
    public async Task<IActionResult> UpdateBankReconciliation(Guid id, [FromBody] UpdateBankReconciliationRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateBankReconciliationAsync(id, request, cancellationToken);
        return NoContent();
    }
}
