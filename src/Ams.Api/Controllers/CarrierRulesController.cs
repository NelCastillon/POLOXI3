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
[Route("api/carriers/rule-lookups")]
public sealed class CarrierRuleLookupsController : ControllerBase
{
    private readonly ICarrierRuleLookupService _service;
    public CarrierRuleLookupsController(ICarrierRuleLookupService service) => _service = service;

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, [FromQuery] string optionType, CancellationToken ct)
        => Ok(await _service.GetOptionsAsync(tenantId, optionType, ct));

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] Guid tenantId, [FromQuery] Guid? carrierId, [FromQuery] Guid? lineOfBusinessId, CancellationToken ct)
        => Ok(await _service.GetProductsAsync(tenantId, carrierId, lineOfBusinessId, ct));
}

[ApiController]
[Route("api/carriers/rule-categories")]
public sealed class CarrierRuleCategoriesController : ControllerBase
{
    private readonly ICarrierRuleCategoryService _service;
    public CarrierRuleCategoriesController(ICarrierRuleCategoryService service) => _service = service;
    [HttpGet] public async Task<IActionResult> GetActive(CancellationToken ct) => Ok(await _service.GetActiveAsync(ct));
}

[ApiController]
[Route("api/carriers/product-rules")]
public sealed class CarrierProductRulesController : ControllerBase
{
    private readonly ICarrierProductRuleService _service;
    public CarrierProductRulesController(ICarrierProductRuleService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId, CancellationToken ct) => (await _service.GetByIdAsync(tenantId, id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? categoryCode, [FromQuery] bool? isActive, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, categoryCode, isActive, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateCarrierProductRuleRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id, tenantId = request.TenantId }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromQuery] Guid tenantId, [FromBody] UpdateCarrierProductRuleRequest request, CancellationToken ct) { await _service.UpdateAsync(tenantId, id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid tenantId, CancellationToken ct) { await _service.DeleteAsync(tenantId, id, ct); return NoContent(); }
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
