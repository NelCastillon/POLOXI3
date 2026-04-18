using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FinanceController : ControllerBase
{
    private readonly IFinanceService _service;
    public FinanceController(IFinanceService service) => _service = service;

    [HttpGet("glaccounts/{id:guid}")]
    public async Task<IActionResult> GetGLAccountById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetGLAccountByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("glaccounts")]
    public async Task<IActionResult> SearchGLAccounts([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchGLAccountsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("journalentries/{id:guid}")]
    public async Task<IActionResult> GetJournalEntryById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetJournalEntryByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("journalentries")]
    public async Task<IActionResult> SearchJournalEntries([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchJournalEntriesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
