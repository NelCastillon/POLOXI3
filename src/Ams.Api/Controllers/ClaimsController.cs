using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/claims")]
public sealed class ClaimsController : ControllerBase
{
    private readonly IClaimsService _service;
    public ClaimsController(IClaimsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? status, [FromQuery] string? lob, [FromQuery] string? catCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
        => TryTenant(tenantId, out var denied) ? Ok(await _service.SearchAsync(tenantId, searchTerm, status, lob, catCode, pageNumber, pageSize, cancellationToken)) : denied;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!TryTenant(tenantId, out var denied)) return denied;
        var item = await _service.GetDetailAsync(tenantId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetOptions([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => TryTenant(tenantId, out var denied) ? Ok(await _service.GetOptionsAsync(tenantId, cancellationToken)) : denied;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClaimRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        request.CreatedByUserId = GetUserId();
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { id, tenantId = request.TenantId }, new { Id = id });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateClaimStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        request.ModifiedByUserId = GetUserId();
        await _service.UpdateStatusAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/follow-up")]
    public async Task<IActionResult> UpdateFollowUp(Guid id, [FromBody] UpdateClaimFollowUpRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        request.ModifiedByUserId = GetUserId();
        await _service.UpdateFollowUpAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("activity")]
    public async Task<IActionResult> AddActivity([FromBody] CreateClaimActivityRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        request.CreatedBy = GetUserName();
        return Ok(new { Id = await _service.AddActivityAsync(request, cancellationToken) });
    }

    [HttpPost("{claimId:guid}/adjusters")]
    public async Task<IActionResult> AssignAdjuster(Guid claimId, [FromBody] AssignClaimAdjusterRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.AssignAdjusterAsync(request with { ClaimId = claimId, UserId = GetUserId() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPut("{claimId:guid}/parties")]
    public async Task<IActionResult> UpsertParty(Guid claimId, [FromBody] UpsertClaimPartyRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.UpsertPartyAsync(request with { ClaimId = claimId, UserId = GetUserId() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPost("{claimId:guid}/financial-transactions")]
    public async Task<IActionResult> CreateFinancialTransaction(Guid claimId, [FromBody] CreateClaimFinancialTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.CreateFinancialTransactionAsync(request with { ClaimId = claimId, UserId = GetUserId() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPost("financial-transactions/{transactionId:guid}/reverse")]
    public async Task<IActionResult> ReverseFinancialTransaction(Guid transactionId, [FromBody] ReverseClaimFinancialTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.ReverseFinancialTransactionAsync(request with { ClaimFinancialTransactionId = transactionId, UserId = GetUserId() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPost("{claimId:guid}/notes")]
    public async Task<IActionResult> CreateNote(Guid claimId, [FromBody] CreateClaimNoteRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.CreateNoteAsync(request with { ClaimId = claimId, UserId = GetUserId(), UserName = GetUserName() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPost("{claimId:guid}/tasks")]
    public async Task<IActionResult> CreateTask(Guid claimId, [FromBody] CreateClaimTaskRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.CreateTaskAsync(request with { ClaimId = claimId, UserId = GetUserId() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTask(Guid taskId, [FromBody] CompleteClaimTaskRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        await _service.CompleteTaskAsync(request with { ClaimTaskId = taskId, UserId = GetUserId() }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{claimId:guid}/documents")]
    public async Task<IActionResult> LinkDocument(Guid claimId, [FromBody] LinkClaimDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        var id = await _service.LinkDocumentAsync(request with { ClaimId = claimId, UserId = GetUserId() }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpGet("loss-runs")]
    public async Task<IActionResult> GetLossRuns([FromQuery] Guid tenantId, [FromQuery] Guid? accountId, CancellationToken cancellationToken)
        => TryTenant(tenantId, out var denied) ? Ok(await _service.GetLossRunsAsync(tenantId, accountId, cancellationToken)) : denied;

    [HttpPost("loss-runs/import")]
    public async Task<IActionResult> ImportLossRun([FromBody] ImportLossRunRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        return Ok(await _service.ImportLossRunAsync(request with { UserId = GetUserId() }, cancellationToken));
    }

    [HttpGet("cat/events")]
    public async Task<IActionResult> SearchCatEvents([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => TryTenant(tenantId, out var denied) ? Ok(await _service.SearchCatEventsAsync(tenantId, cancellationToken)) : denied;

    [HttpPost("cat/events")]
    public async Task<IActionResult> CreateCatEvent([FromBody] CreateCatEventRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        return Ok(new { Id = await _service.CreateCatEventAsync(request, cancellationToken) });
    }

    [HttpGet("cat/page")]
    public async Task<IActionResult> GetCatastrophePage([FromQuery] Guid tenantId, [FromQuery] Guid? catEventId, CancellationToken cancellationToken)
        => TryTenant(tenantId, out var denied) ? Ok(await _service.GetCatastrophePageAsync(tenantId, catEventId, cancellationToken)) : denied;

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
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        request.CatEventId = id;
        request.SentBy = GetUserName();
        return Ok(new { Count = await _service.SendCatBlastAsync(request, cancellationToken) });
    }

    [HttpPost("cat/events/{id:guid}/fast-fnol")]
    public async Task<IActionResult> CreateFastCatFnol(Guid id, [FromBody] FastCatFnolRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(request.TenantId, out var denied)) return denied;
        request.CatEventId = id;
        request.CreatedByUserId = GetUserId();
        request.CreatedByName = GetUserName();
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

    private bool TryTenant(Guid requestedTenantId, out IActionResult denied)
    {
        denied = Forbid();
        var claim = User.FindFirstValue("tenant_id") ?? User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return requestedTenantId != Guid.Empty && Guid.TryParse(claim, out var authenticatedTenantId) && authenticatedTenantId == requestedTenantId;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("userId");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private string GetUserName()
        => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? User.FindFirstValue("preferred_username") ?? "Authenticated User";
}
