using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.CarrierConfig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/carriers/mgas")]
public sealed class MgaWholesalersController : ControllerBase
{
    private readonly IMgaWholesalerService _service;
    public MgaWholesalersController(IMgaWholesalerService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateMgaWholesalerRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMgaWholesalerRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/carriers/settings")]
public sealed class CarrierSettingsController : ControllerBase
{
    private readonly ICarrierSettingService _service;
    public CarrierSettingsController(ICarrierSettingService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateCarrierSettingRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierSettingRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Authorize]
[Route("api/carriers/contacts")]
public sealed class CarrierContactsController : ControllerBase
{
    private readonly ICarrierContactService _service;
    public CarrierContactsController(ICarrierContactService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpGet("by-carrier/{carrierId:guid}")]
    public async Task<IActionResult> GetActiveByCarrier(Guid carrierId, [FromQuery] Guid tenantId, CancellationToken ct = default)
    {
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return tenantId != Guid.Empty && carrierId != Guid.Empty && Guid.TryParse(claim, out var authenticatedTenantId) && authenticatedTenantId == tenantId
            ? Ok(await _service.GetActiveByCarrierAsync(tenantId, carrierId, ct))
            : Forbid();
    }
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateCarrierContactRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierContactRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/carriers/appointments")]
public sealed class CarrierAppointmentsController : ControllerBase
{
    private readonly ICarrierAppointmentService _service;
    public CarrierAppointmentsController(ICarrierAppointmentService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateCarrierAppointmentRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierAppointmentRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController]
[Route("api/carriers/performance")]
public sealed class CarrierPerformanceController : ControllerBase
{
    private readonly ICarrierPerformanceService _service;
    public CarrierPerformanceController(ICarrierPerformanceService service) => _service = service;
    [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(id, ct)) is { } item ? Ok(item) : NotFound();
    [HttpGet] public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] CreateCarrierPerformanceRequest request, CancellationToken ct) { var id = await _service.CreateAsync(request, ct); return CreatedAtAction(nameof(GetById), new { id }, new { id }); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCarrierPerformanceRequest request, CancellationToken ct) { await _service.UpdateAsync(id, request, ct); return NoContent(); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return NoContent(); }
}
