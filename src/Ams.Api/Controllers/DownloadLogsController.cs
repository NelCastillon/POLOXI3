using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Integrations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/downloads")]
public sealed class DownloadLogsController : ControllerBase
{
    private readonly IIntegrationService _service;
    public DownloadLogsController(IIntegrationService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.GetDownloadLogsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
        => Ok(await _service.GetCarrierDownloadDashboardAsync(tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDownloadLogByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreateCarrierDownloadBatchRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateCarrierDownloadBatchAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { Id = id });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteBatch(Guid id, [FromBody] CompleteCarrierDownloadBatchRequest request, CancellationToken cancellationToken)
    {
        await _service.CompleteCarrierDownloadBatchAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] Guid tenantId, [FromQuery] Guid? batchId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        => Ok(await _service.GetCarrierDownloadItemsAsync(tenantId, batchId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItemById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetCarrierDownloadItemByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] CreateCarrierDownloadItemRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateCarrierDownloadItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetItemById), new { id }, new { Id = id });
    }

    [HttpPost("items/{id:guid}/status")]
    public async Task<IActionResult> UpdateItemStatus(Guid id, [FromBody] UpdateCarrierDownloadItemStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateCarrierDownloadItemStatusAsync(id, request, cancellationToken);
        return NoContent();
    }
}
