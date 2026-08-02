using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ESignController : ControllerBase
{
    private readonly IESignService _service;
    public ESignController(IESignService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByTenant([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanViewPolicy(User, tenantId) ? Ok(await _service.GetByTenantAsync(tenantId, cancellationToken)) : Forbid();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPolicy(User, tenantId)) return Forbid();
        var item = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendESignRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        var id = await _service.SendAsync(request with { RequestedByUserId = AuthenticatedRequestContext.GetUserId(User) }, cancellationToken);
        return Ok(new { Id = id });
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidESignRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        await _service.VoidAsync(request with { ESignRequestId = id, ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User) }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/remind")]
    public async Task<IActionResult> Remind(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, tenantId)) return Forbid();
        await _service.RemindAsync(tenantId, id, AuthenticatedRequestContext.GetUserId(User), cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("callbacks/docusign/{tenantId:guid}")]
    public async Task<IActionResult> DocuSignCallback(Guid tenantId, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-DocuSign-Signature-1"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(signature)) return Unauthorized();
        try
        {
            await _service.ProcessDocuSignCallbackAsync(new ProcessDocuSignCallbackRequest(tenantId, payload, signature), cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
