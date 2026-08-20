using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/address-location")]
public sealed class AddressLocationController : ControllerBase
{
    private readonly IAddressLocationService _service;

    public AddressLocationController(IAddressLocationService service)
    {
        _service = service;
    }

    [HttpGet("status")]
    public async Task<ActionResult<AddressEngineStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        return Ok(await _service.GetStatusAsync(tenantId.Value, cancellationToken));
    }

    [HttpGet("autocomplete")]
    public async Task<ActionResult<IReadOnlyList<AddressSuggestionDto>>> Autocomplete(
        [FromQuery] string query,
        [FromQuery] string? countrySet,
        [FromQuery] string? language,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();

        var status = await _service.GetStatusAsync(tenantId.Value, cancellationToken);
        if (!status.IsAvailable) return StatusCode(StatusCodes.Status503ServiceUnavailable, status);

        try
        {
            return Ok(await _service.AutocompleteAsync(new AddressAutocompleteRequest
            {
                TenantId = tenantId.Value,
                Query = query,
                CountrySet = countrySet,
                Language = language,
                Limit = limit
            }, cancellationToken));
        }
        catch (AddressProviderUnavailableException ex)
        {
            return ProviderUnavailable(ex.Message);
        }
    }

    [HttpPost("geocode")]
    public async Task<ActionResult<AddressSuggestionDto>> Geocode(
        [FromBody] AddressGeocodeRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        request.TenantId = tenantId.Value;

        var status = await _service.GetStatusAsync(tenantId.Value, cancellationToken);
        if (!status.IsAvailable) return StatusCode(StatusCodes.Status503ServiceUnavailable, status);

        try
        {
            var result = await _service.GeocodeAsync(request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AddressProviderUnavailableException ex)
        {
            return ProviderUnavailable(ex.Message);
        }
    }

    [HttpPost("resolutions")]
    public async Task<ActionResult<Guid>> PersistResolution(
        [FromBody] PersistAddressResolutionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        request.TenantId = tenantId.Value;
        request.UserId = AuthenticatedRequestContext.GetUserId(User);
        return Ok(await _service.PersistResolutionAsync(request, cancellationToken));
    }

    [HttpGet("geo/states")]
    public async Task<ActionResult<IReadOnlyList<GeoStateDto>>> GetStates(
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        return Ok(await _service.GetStatesAsync(countryCode, cancellationToken));
    }

    [HttpGet("geo/cities")]
    public async Task<ActionResult<IReadOnlyList<GeoCityDto>>> SearchCities(
        [FromQuery] string query,
        [FromQuery] string? countryCode,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        return Ok(await _service.SearchCitiesAsync(countryCode, query, limit ?? 10, cancellationToken));
    }

    [HttpGet("geo/postalcodes")]
    public async Task<ActionResult<IReadOnlyList<GeoPostalCodeDto>>> GetPostalCodes(
        [FromQuery] string stateCode,
        [FromQuery] string cityName,
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        return Ok(await _service.GetPostalCodesAsync(countryCode, stateCode, cityName, cancellationToken));
    }

    [HttpGet("configuration")]
    public async Task<ActionResult<AddressProviderConfigurationDto>> GetConfiguration(CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        if (!AuthenticatedRequestContext.CanManageAccounts(User, tenantId.Value)) return Forbid();

        var configuration = await _service.GetConfigurationAsync(tenantId.Value, cancellationToken);
        return configuration is null ? NotFound() : Ok(configuration);
    }

    [HttpPut("configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        [FromBody] UpdateAddressProviderConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = AuthenticatedRequestContext.GetTenantId(User);
        if (!tenantId.HasValue) return Forbid();
        if (!AuthenticatedRequestContext.CanManageAccounts(User, tenantId.Value)) return Forbid();

        request.TenantId = tenantId.Value;
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.UpdateConfigurationAsync(request, cancellationToken);
        return NoContent();
    }

    private ObjectResult ProviderUnavailable(string detail) => StatusCode(
        StatusCodes.Status503ServiceUnavailable,
        new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Address provider unavailable",
            Detail = detail,
            Instance = Request.Path
        });
}
