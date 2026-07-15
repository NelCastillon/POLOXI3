using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Opportunities;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OpportunitiesController : ControllerBase
{
    private readonly IOpportunityService _service;

    public OpportunitiesController(IOpportunityService service)
    {
        _service = service;
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDetailAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/conversion-launch")]
    public async Task<IActionResult> GetConversionLaunch(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetConversionLaunchAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("competitor-lookups")]
    public async Task<IActionResult> SearchCompetitorLookups([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchCompetitorLookupsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOpportunityRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/stage")]
    public async Task<IActionResult> UpdateStage(Guid id, [FromBody] Ams.Application.Features.Opportunities.UpdateOpportunityStageRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateStageAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<IActionResult> UpsertLine(Guid id, [FromBody] UpsertOpportunityLineRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        return Ok(await _service.UpsertLineAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    public async Task<IActionResult> UpdateLine(Guid id, Guid lineId, [FromBody] UpsertOpportunityLineRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        request.OpportunityLineId = lineId;
        return Ok(await _service.UpsertLineAsync(request, cancellationToken));
    }

    [HttpPatch("{id:guid}/lines/{lineId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryLine(Guid id, Guid lineId, [FromBody] SetPrimaryOpportunityLineRequest request, CancellationToken cancellationToken)
    {
        await _service.SetPrimaryLineAsync(id, lineId, request.UserId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("lines/{lineId:guid}")]
    public async Task<IActionResult> DeleteLine(Guid lineId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteLineAsync(lineId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/activities")]
    public async Task<IActionResult> UpsertActivity(Guid id, [FromBody] UpsertOpportunityActivityRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        return Ok(await _service.UpsertActivityAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}/activities/{activityId:guid}")]
    public async Task<IActionResult> UpdateActivity(Guid id, Guid activityId, [FromBody] UpsertOpportunityActivityRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        request.ActivityId = activityId;
        return Ok(await _service.UpsertActivityAsync(request, cancellationToken));
    }

    [HttpDelete("activities/{activityId:guid}")]
    public async Task<IActionResult> DeleteActivity(Guid activityId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteActivityAsync(activityId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/submissions")]
    public async Task<IActionResult> UpsertSubmission(Guid id, [FromBody] UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        return Ok(await _service.UpsertSubmissionAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}/submissions/{submissionId:guid}")]
    public async Task<IActionResult> UpdateSubmission(Guid id, Guid submissionId, [FromBody] UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        request.SubmissionId = submissionId;
        return Ok(await _service.UpsertSubmissionAsync(request, cancellationToken));
    }

    [HttpDelete("submissions/{submissionId:guid}")]
    public async Task<IActionResult> DeleteSubmission(Guid submissionId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteSubmissionAsync(submissionId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/competitors")]
    public async Task<IActionResult> UpsertCompetitor(Guid id, [FromBody] UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        return Ok(await _service.UpsertCompetitorAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}/competitors/{competitorId:guid}")]
    public async Task<IActionResult> UpdateCompetitor(Guid id, Guid competitorId, [FromBody] UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken)
    {
        request.OpportunityId = id;
        request.CompetitorId = competitorId;
        return Ok(await _service.UpsertCompetitorAsync(request, cancellationToken));
    }

    [HttpDelete("competitors/{competitorId:guid}")]
    public async Task<IActionResult> DeleteCompetitor(Guid competitorId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteCompetitorAsync(competitorId, modifiedByUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
}
