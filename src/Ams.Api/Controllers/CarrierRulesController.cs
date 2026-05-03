using Ams.Application.Abstractions.Services;
using Ams.Application.Features.CarrierRules;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/carriers/access-rules")]
public sealed class MarketAccessRulesController : ControllerBase
{
    private readonly IMarketAccessRuleService _service;
    public MarketAccessRulesController(IMarketAccessRuleService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateMarketAccessRuleRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMarketAccessRuleRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/carriers/download-mappings")]
public sealed class CarrierDownloadMappingsController : ControllerBase
{
    private readonly ICarrierDownloadMappingService _service;
    public CarrierDownloadMappingsController(ICarrierDownloadMappingService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateCarrierDownloadMappingRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierDownloadMappingRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}
