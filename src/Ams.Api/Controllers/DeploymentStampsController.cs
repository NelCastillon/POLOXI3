using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DeploymentStamps;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/deployment-stamps")]
public sealed class DeploymentStampsController : ControllerBase
{
    private readonly IDeploymentStampService _service;

    public DeploymentStampsController(IDeploymentStampService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(searchTerm, statusCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var stamp = await _service.GetByIdAsync(id, cancellationToken);
        return stamp is null ? NotFound() : Ok(stamp);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeploymentStampRequest request, CancellationToken cancellationToken = default)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeploymentStampRequest request, CancellationToken cancellationToken = default)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromQuery] string statusCode, CancellationToken cancellationToken = default)
    {
        await _service.SetStatusAsync(id, statusCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
