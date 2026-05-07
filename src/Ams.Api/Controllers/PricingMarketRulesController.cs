using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/crm/pricing-market-rules")]
public sealed class PricingMarketRulesController : ControllerBase
{
    private readonly IPricingMarketRulesService _service;

    public PricingMarketRulesController(IPricingMarketRulesService service) => _service = service;

    [HttpGet("classes")]
    public async Task<IActionResult> SearchClasses([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 250, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchPriceClassesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] UpsertPriceClassRequest request, CancellationToken cancellationToken)
        => Ok(new IdResult { Id = await _service.CreatePriceClassAsync(request, cancellationToken) });

    [HttpPut("classes/{id:guid}")]
    public async Task<IActionResult> UpdateClass(Guid id, [FromBody] UpsertPriceClassRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdatePriceClassAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("classes/{id:guid}")]
    public async Task<IActionResult> DeleteClass(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeletePriceClassAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("appetite")]
    public async Task<IActionResult> SearchAppetite([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 250, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchMarketAppetiteAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("appetite")]
    public async Task<IActionResult> CreateAppetite([FromBody] UpsertMarketAppetiteRequest request, CancellationToken cancellationToken)
        => Ok(new IdResult { Id = await _service.CreateMarketAppetiteAsync(request, cancellationToken) });

    [HttpPut("appetite/{id:guid}")]
    public async Task<IActionResult> UpdateAppetite(Guid id, [FromBody] UpsertMarketAppetiteRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateMarketAppetiteAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("appetite/{id:guid}")]
    public async Task<IActionResult> DeleteAppetite(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteMarketAppetiteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("carrier-mappings")]
    public async Task<IActionResult> SearchCarrierMappings([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 250, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchCarrierMappingsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost("carrier-mappings")]
    public async Task<IActionResult> CreateCarrierMapping([FromBody] UpsertCarrierMappingRequest request, CancellationToken cancellationToken)
        => Ok(new IdResult { Id = await _service.CreateCarrierMappingAsync(request, cancellationToken) });

    [HttpPut("carrier-mappings/{id:guid}")]
    public async Task<IActionResult> UpdateCarrierMapping(Guid id, [FromBody] UpsertCarrierMappingRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateCarrierMappingAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("carrier-mappings/{id:guid}")]
    public async Task<IActionResult> DeleteCarrierMapping(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteCarrierMappingAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("carrier-mappings/{id:guid}/test")]
    public async Task<IActionResult> TestCarrierMapping(Guid id, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.TestCarrierMappingAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
