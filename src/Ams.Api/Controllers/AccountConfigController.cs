using Ams.Application.Abstractions.Services;
using Ams.Application.Features.AccountConfig;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/accounts/types")]
public sealed class AccountTypesController : ControllerBase
{
    private readonly IAccountTypeService _service;
    public AccountTypesController(IAccountTypeService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountTypeRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountTypeRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/accounts/rel-types")]
public sealed class RelationshipTypesController : ControllerBase
{
    private readonly IRelationshipTypeService _service;
    public RelationshipTypesController(IRelationshipTypeService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRelationshipTypeRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRelationshipTypeRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/accounts/reference-options")]
public sealed class AccountReferenceOptionsController : ControllerBase
{
    private readonly IAccountReferenceOptionService _service;
    public AccountReferenceOptionsController(IAccountReferenceOptionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid tenantId, [FromQuery] string? optionGroup = null, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(tenantId, optionGroup, ct));
}

[ApiController]
[Route("api/accounts/household-settings")]
public sealed class HouseholdSettingsController : ControllerBase
{
    private readonly IHouseholdSettingService _service;
    public HouseholdSettingsController(IHouseholdSettingService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _service.GetAllAsync(tenantId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHouseholdSettingRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/accounts/commercial-settings")]
public sealed class CommercialEntitySettingsController : ControllerBase
{
    private readonly ICommercialEntitySettingService _service;
    public CommercialEntitySettingsController(ICommercialEntitySettingService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _service.GetAllAsync(tenantId, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommercialEntitySettingRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/accounts/contact-types")]
public sealed class ContactTypesController : ControllerBase
{
    private readonly IContactTypeService _service;
    public ContactTypesController(IContactTypeService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactTypeRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactTypeRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/accounts/custom-fields")]
public sealed class AccountCustomFieldsController : ControllerBase
{
    private readonly IAccountCustomFieldService _service;
    public AccountCustomFieldsController(IAccountCustomFieldService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountCustomFieldRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountCustomFieldRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
