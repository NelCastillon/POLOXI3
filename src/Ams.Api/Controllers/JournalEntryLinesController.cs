using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/finance/journal-entry-lines")]
public sealed class JournalEntryLinesController : ControllerBase
{
    private readonly IJournalEntryLineService _service;
    public JournalEntryLinesController(IJournalEntryLineService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> GetByJournalEntry([FromQuery] Guid journalEntryId, CancellationToken cancellationToken)
        => Ok(await _service.GetByJournalEntryIdAsync(journalEntryId, cancellationToken));
}
