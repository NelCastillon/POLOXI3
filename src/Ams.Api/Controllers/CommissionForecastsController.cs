using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Commissions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/commissions/forecasts")]
public sealed class CommissionForecastsController : ControllerBase
{
    private readonly ICommissionForecastService _service;

    public CommissionForecastsController(ICommissionForecastService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? statusCode, [FromQuery] string? scenarioCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, statusCode, scenarioCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommissionForecastRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommissionForecastRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("seed")]
    public async Task<IActionResult> EnsureSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await _service.EnsureSeedAsync(tenantId, cancellationToken);
        return NoContent();
    }
}
