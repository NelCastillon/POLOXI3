using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/claims")]
public sealed class ClaimsController : ControllerBase
{
    private readonly IClaimsService _service;
    public ClaimsController(IClaimsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? status, [FromQuery] string? lob, [FromQuery] string? catCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, status, lob, catCode, pageNumber, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDetailAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClaimRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateAsync(request, cancellationToken) });

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateClaimStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/follow-up")]
    public async Task<IActionResult> UpdateFollowUp(Guid id, [FromBody] UpdateClaimFollowUpRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateFollowUpAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activity")]
    public async Task<IActionResult> AddActivity([FromBody] CreateClaimActivityRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });

    [HttpGet("cat/events")]
    public async Task<IActionResult> SearchCatEvents([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.SearchCatEventsAsync(tenantId, cancellationToken));

    [HttpPost("cat/events")]
    public async Task<IActionResult> CreateCatEvent([FromBody] CreateCatEventRequest request, CancellationToken cancellationToken)
        => Ok(new { Id = await _service.CreateCatEventAsync(request, cancellationToken) });

    [HttpGet("cat/page")]
    public async Task<IActionResult> GetCatastrophePage([FromQuery] Guid tenantId, [FromQuery] Guid? catEventId, CancellationToken cancellationToken)
        => Ok(await _service.GetCatastrophePageAsync(tenantId, catEventId, cancellationToken));

    [HttpPatch("cat/affected/{id:guid}/contacted")]
    public async Task<IActionResult> MarkAffectedInsuredContacted(Guid id, CancellationToken cancellationToken)
    {
        await _service.MarkAffectedInsuredContactedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("cat/events/{id:guid}/geo-tag")]
    public async Task<IActionResult> ApplyGeoTag(Guid id, [FromBody] GeoTagRequest request, CancellationToken cancellationToken)
        => Ok(new { Count = await _service.ApplyGeoTagAsync(id, request.States, request.Counties, request.Zips, request.Lob, request.MinTiv, cancellationToken) });

    [HttpPost("cat/events/{id:guid}/blast")]
    public async Task<IActionResult> SendCatBlast(Guid id, [FromBody] CatBlastRequest request, CancellationToken cancellationToken)
    {
        request.CatEventId = id;
        return Ok(new { Count = await _service.SendCatBlastAsync(request, cancellationToken) });
    }

    [HttpPost("cat/events/{id:guid}/fast-fnol")]
    public async Task<IActionResult> CreateFastCatFnol(Guid id, [FromBody] FastCatFnolRequest request, CancellationToken cancellationToken)
    {
        request.CatEventId = id;
        return Ok(new { Id = await _service.CreateFastCatFnolAsync(request, cancellationToken) });
    }

    public sealed class GeoTagRequest
    {
        public string? States { get; set; }
        public string? Counties { get; set; }
        public string? Zips { get; set; }
        public string? Lob { get; set; }
        public decimal? MinTiv { get; set; }
    }
}
