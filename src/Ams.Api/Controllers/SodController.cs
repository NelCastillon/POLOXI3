using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Sod;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/sod")]
public sealed class SodController : ControllerBase
{
    private readonly ISodRuleService     _sodRuleService;
    private readonly ISodConflictService _sodConflictService;

    public SodController(ISodRuleService sodRuleService, ISodConflictService sodConflictService)
    {
        _sodRuleService     = sodRuleService;
        _sodConflictService = sodConflictService;
    }

    // ── SoD Rules ─────────────────────────────────────────────────────────────

    [HttpGet("rules")]
    public async Task<IActionResult> SearchRules(
        [FromQuery] Guid?   tenantId     = null,
        [FromQuery] string? searchTerm   = null,
        [FromQuery] string? severityCode = null,
        [FromQuery] bool?   isActive     = null,
        [FromQuery] int     pageNumber   = 1,
        [FromQuery] int     pageSize     = 25,
        CancellationToken   ct           = default)
        => Ok(await _sodRuleService.SearchAsync(tenantId, searchTerm, severityCode, isActive, pageNumber, pageSize, ct));

    [HttpGet("rules/{id:guid}")]
    public async Task<IActionResult> GetRuleById(Guid id, CancellationToken ct)
    {
        var rule = await _sodRuleService.GetByIdAsync(id, ct);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateSodRuleRequest request, CancellationToken ct)
    {
        var id = await _sodRuleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetRuleById), new { id }, new { id });
    }

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateSodRuleRequest request, CancellationToken ct)
    {
        await _sodRuleService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPatch("rules/{id:guid}/activate")]
    public async Task<IActionResult> ActivateRule(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken ct)
    {
        await _sodRuleService.SetActiveAsync(id, true, modifiedByUserId, ct);
        return NoContent();
    }

    [HttpPatch("rules/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRule(Guid id, [FromQuery] Guid? modifiedByUserId, CancellationToken ct)
    {
        await _sodRuleService.SetActiveAsync(id, false, modifiedByUserId, ct);
        return NoContent();
    }

    [HttpPost("rules/{id:guid}/clone")]
    public async Task<IActionResult> CloneRule(Guid id, [FromBody] CloneSodRuleRequest request, CancellationToken ct)
    {
        var newId = await _sodRuleService.CloneAsync(id, request, ct);
        return CreatedAtAction(nameof(GetRuleById), new { id = newId }, new { id = newId });
    }

    // ── SoD Conflicts ──────────────────────────────────────────────────────────

    [HttpGet("conflicts")]
    public async Task<IActionResult> SearchConflicts(
        [FromQuery] Guid?   tenantId     = null,
        [FromQuery] string? searchTerm   = null,
        [FromQuery] string? statusCode   = null,
        [FromQuery] string? severityCode = null,
        [FromQuery] int     pageNumber   = 1,
        [FromQuery] int     pageSize     = 25,
        CancellationToken   ct           = default)
        => Ok(await _sodConflictService.SearchAsync(tenantId, searchTerm, statusCode, severityCode, pageNumber, pageSize, ct));

    [HttpGet("conflicts/{id:guid}")]
    public async Task<IActionResult> GetConflictById(Guid id, CancellationToken ct)
    {
        var item = await _sodConflictService.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPatch("conflicts/{id:guid}/assign-reviewer")]
    public async Task<IActionResult> AssignReviewer(Guid id, [FromBody] AssignSodConflictReviewerRequest request, CancellationToken ct)
    {
        await _sodConflictService.AssignReviewerAsync(id, request, ct);
        return NoContent();
    }

    [HttpPatch("conflicts/{id:guid}/remediate")]
    public async Task<IActionResult> Remediate(Guid id, [FromBody] RemediateSodConflictRequest request, CancellationToken ct)
    {
        await _sodConflictService.RemediateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPatch("conflicts/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveSodConflictRequest request, CancellationToken ct)
    {
        await _sodConflictService.ResolveAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("conflicts/{id:guid}/exception")]
    public async Task<IActionResult> CreateException(Guid id, [FromBody] CreateSodExceptionRequest request, CancellationToken ct)
    {
        await _sodConflictService.CreateExceptionAsync(id, request, ct);
        return NoContent();
    }
}
