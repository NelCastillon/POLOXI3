using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Iam;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/profile")]
public sealed class UserProfileController : ControllerBase
{
    private readonly IUserProfileService _service;
    public UserProfileController(IUserProfileService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _service.GetByUserIdAsync(userId, cancellationToken);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(Guid userId, [FromBody] UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        request.UserId = userId;
        await _service.UpsertAsync(request, cancellationToken);
        return NoContent();
    }
}
