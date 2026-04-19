using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/plans")]
public sealed class PlansController : ControllerBase
{
    private readonly IPlanService _service;
    private readonly IPlanSubEntityService _subService;

    public PlansController(IPlanService service, IPlanSubEntityService subService)
    {
        _service    = service;
        _subService = subService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, [FromBody] ClonePlanRequest request, CancellationToken cancellationToken)
    {
        await _service.CloneAsync(id, request.NewPlanCode, request.NewPlanName, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SetActiveAsync(id, true, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _service.SetActiveAsync(id, false, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    // ── Features ─────────────────────────────────────────────
    [HttpGet("{id:guid}/features")]
    public async Task<IActionResult> GetFeatures(Guid id, CancellationToken cancellationToken)
        => Ok(await _subService.GetFeaturesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/features")]
    public async Task<IActionResult> AddFeature(Guid id, [FromBody] AddPlanFeatureRequest request, CancellationToken cancellationToken)
    {
        request.PlanId = id;
        var newId = await _subService.AddFeatureAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetFeatures), new { id }, new { id = newId });
    }

    [HttpDelete("{id:guid}/features/{planFeatureId:guid}")]
    public async Task<IActionResult> RemoveFeature(Guid id, Guid planFeatureId, CancellationToken cancellationToken)
    {
        await _subService.RemoveFeatureAsync(planFeatureId, cancellationToken);
        return NoContent();
    }

    // ── Limits ───────────────────────────────────────────────
    [HttpGet("{id:guid}/limits")]
    public async Task<IActionResult> GetLimits(Guid id, CancellationToken cancellationToken)
        => Ok(await _subService.GetLimitsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/limits")]
    public async Task<IActionResult> AddLimit(Guid id, [FromBody] AddPlanLimitRequest request, CancellationToken cancellationToken)
    {
        request.PlanId = id;
        var newId = await _subService.AddLimitAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetLimits), new { id }, new { id = newId });
    }

    [HttpPut("{id:guid}/limits/{planLimitId:guid}")]
    public async Task<IActionResult> UpdateLimit(Guid id, Guid planLimitId, [FromBody] UpdatePlanLimitRequest request, CancellationToken cancellationToken)
    {
        request.PlanLimitId = planLimitId;
        await _subService.UpdateLimitAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/limits/{planLimitId:guid}")]
    public async Task<IActionResult> RemoveLimit(Guid id, Guid planLimitId, CancellationToken cancellationToken)
    {
        await _subService.RemoveLimitAsync(planLimitId, cancellationToken);
        return NoContent();
    }

    // ── Add-Ons ──────────────────────────────────────────────
    [HttpGet("{id:guid}/addons")]
    public async Task<IActionResult> GetAddOns(Guid id, CancellationToken cancellationToken)
        => Ok(await _subService.GetAddOnsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/addons")]
    public async Task<IActionResult> AddAddOn(Guid id, [FromBody] AddPlanAddOnRequest request, CancellationToken cancellationToken)
    {
        request.PlanId = id;
        var newId = await _subService.AddAddOnAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAddOns), new { id }, new { id = newId });
    }

    [HttpDelete("{id:guid}/addons/{planAddOnId:guid}")]
    public async Task<IActionResult> RemoveAddOn(Guid id, Guid planAddOnId, CancellationToken cancellationToken)
    {
        await _subService.RemoveAddOnAsync(planAddOnId, cancellationToken);
        return NoContent();
    }
}

public sealed class ClonePlanRequest
{
    public string NewPlanCode { get; set; } = string.Empty;
    public string NewPlanName { get; set; } = string.Empty;
}
