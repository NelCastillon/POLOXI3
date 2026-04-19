using Ams.Application.Abstractions.Services;
using Ams.Application.Features.SlaDefinitions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/sla-definitions")]
public sealed class SlaDefinitionsController : ControllerBase
{
    private readonly ISlaDefinitionService _service;

    public SlaDefinitionsController(ISlaDefinitionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm       = null,
        [FromQuery] string? complianceStatus = null,
        [FromQuery] int     pageNumber       = 1,
        [FromQuery] int     pageSize         = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, complianceStatus, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { Id = id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
