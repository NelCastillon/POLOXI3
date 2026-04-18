using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LeadsController : ControllerBase
{
    private readonly ILeadService _service;

    public LeadsController(ILeadService service)
    {
        _service = service;
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Ams.Application.Features.Leads.CreateLeadRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
}
