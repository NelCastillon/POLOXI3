using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/platform/configuration")]
public sealed class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _service;
    public ConfigurationController(IConfigurationService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("key")]
    public async Task<IActionResult> GetByKey([FromQuery] string settingKey, [FromQuery] string scopeCode, [FromQuery] Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var item = await _service.GetByKeyAsync(settingKey, scopeCode, tenantId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm, [FromQuery] string? scopeCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(searchTerm, scopeCode, pageNumber, pageSize, cancellationToken));

    [HttpGet("scope/{scopeCode}")]
    public async Task<IActionResult> GetByScope(string scopeCode, CancellationToken cancellationToken = default)
        => Ok(await _service.GetByScopeAsync(scopeCode, cancellationToken));

    [HttpPut("{id:guid}/value")]
    public async Task<IActionResult> UpdateValue(Guid id, [FromBody] UpdateSettingValueRequest request, CancellationToken cancellationToken = default)
    {
        await _service.UpdateValueAsync(id, request.SettingValue, cancellationToken);
        return NoContent();
    }

    [HttpGet("tenant/{tenantId:guid}")]
    public async Task<IActionResult> GetTenantSettings(Guid tenantId, CancellationToken cancellationToken = default)
        => Ok(await _service.GetTenantSettingsAsync(tenantId, cancellationToken));

    [HttpPut("tenant/{tenantId:guid}/setting")]
    public async Task<IActionResult> UpsertTenantSetting(Guid tenantId, [FromBody] UpsertTenantSettingRequest request, CancellationToken cancellationToken = default)
    {
        await _service.UpsertTenantSettingAsync(tenantId, request.SettingKey, request.SettingValue, cancellationToken);
        return NoContent();
    }

    [HttpPut("tenant/{tenantId:guid}/settings")]
    public async Task<IActionResult> UpsertTenantSettings(Guid tenantId, [FromBody] List<UpsertTenantSettingRequest> requests, CancellationToken cancellationToken = default)
    {
        foreach (var r in requests)
            await _service.UpsertTenantSettingAsync(tenantId, r.SettingKey, r.SettingValue, cancellationToken);
        return NoContent();
    }
}

public sealed class UpdateSettingValueRequest
{
    public string? SettingValue { get; set; }
}

public sealed class UpsertTenantSettingRequest
{
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
}
