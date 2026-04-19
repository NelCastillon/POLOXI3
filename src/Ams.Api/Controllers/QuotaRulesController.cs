using Ams.Application.Abstractions.Services;
using Ams.Application.Features.QuotaRules;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/quota-rules")]
public sealed class QuotaRulesController : ControllerBase
{
    private readonly IQuotaRuleService _service;

    public QuotaRulesController(IQuotaRuleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm  = null,
        [FromQuery] string? planCode    = null,
        [FromQuery] bool?   isActive    = null,
        [FromQuery] int     pageNumber  = 1,
        [FromQuery] int     pageSize    = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, planCode, isActive, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _service.GetByIdAsync(id, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuotaRuleRequest request, CancellationToken cancellationToken = default)
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

    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, [FromBody] CloneQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        var newId = await _service.CloneAsync(id, request, cancellationToken);
        return Ok(new { Id = newId });
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
