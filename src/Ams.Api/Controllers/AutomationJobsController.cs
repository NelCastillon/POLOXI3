using Ams.Application.Abstractions.Services;
using Ams.Application.Features.AutomationJobs;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/automation/jobs")]
public sealed class AutomationJobsController : ControllerBase
{
    private readonly IAutomationJobService _automationJobService;

    public AutomationJobsController(IAutomationJobService automationJobService) => _automationJobService = automationJobService;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetDashboardAsync(tenantId, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> SearchJobDefinitions(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? statusCode,
        [FromQuery] string? categoryCode,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await _automationJobService.SearchJobDefinitionsAsync(tenantId, searchTerm, statusCode, categoryCode, pageNumber, pageSize, cancellationToken));

    [HttpGet("{jobDefinitionId:guid}")]
    public async Task<IActionResult> GetJobDefinition(Guid jobDefinitionId, CancellationToken cancellationToken)
    {
        var job = await _automationJobService.GetJobDefinitionAsync(jobDefinitionId, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("{jobDefinitionId:guid}/steps")]
    public async Task<IActionResult> GetJobSteps(Guid jobDefinitionId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetJobStepsAsync(jobDefinitionId, cancellationToken));

    [HttpGet("{jobDefinitionId:guid}/schedules")]
    public async Task<IActionResult> GetJobSchedules(Guid jobDefinitionId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetJobSchedulesAsync(jobDefinitionId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateJobDefinition([FromBody] CreateJobDefinitionRequest request, CancellationToken cancellationToken)
    {
        var id = await _automationJobService.CreateJobDefinitionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetJobDefinition), new { jobDefinitionId = id }, new IdResult(id));
    }

    [HttpPut("{jobDefinitionId:guid}")]
    public async Task<IActionResult> UpdateJobDefinition(Guid jobDefinitionId, [FromBody] UpdateJobDefinitionRequest request, CancellationToken cancellationToken)
    {
        await _automationJobService.UpdateJobDefinitionAsync(jobDefinitionId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{jobDefinitionId:guid}/status")]
    public async Task<IActionResult> SetJobDefinitionStatus(Guid jobDefinitionId, [FromBody] SetJobDefinitionStatusRequest request, CancellationToken cancellationToken)
    {
        await _automationJobService.SetJobDefinitionStatusAsync(jobDefinitionId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{jobDefinitionId:guid}/steps")]
    public async Task<IActionResult> CreateJobStep(Guid jobDefinitionId, [FromBody] UpsertJobStepRequest request, CancellationToken cancellationToken)
    {
        var id = await _automationJobService.UpsertJobStepAsync(null, request with { JobDefinitionId = jobDefinitionId }, cancellationToken);
        return Ok(new IdResult(id));
    }

    [HttpPut("{jobDefinitionId:guid}/steps/{jobStepId:guid}")]
    public async Task<IActionResult> UpdateJobStep(Guid jobDefinitionId, Guid jobStepId, [FromBody] UpsertJobStepRequest request, CancellationToken cancellationToken)
    {
        var id = await _automationJobService.UpsertJobStepAsync(jobStepId, request with { JobDefinitionId = jobDefinitionId }, cancellationToken);
        return Ok(new IdResult(id));
    }

    [HttpPost("{jobDefinitionId:guid}/schedules")]
    public async Task<IActionResult> CreateJobSchedule(Guid jobDefinitionId, [FromBody] UpsertJobScheduleRequest request, CancellationToken cancellationToken)
    {
        var id = await _automationJobService.UpsertJobScheduleAsync(null, request with { JobDefinitionId = jobDefinitionId }, cancellationToken);
        return Ok(new IdResult(id));
    }

    [HttpPut("{jobDefinitionId:guid}/schedules/{jobScheduleId:guid}")]
    public async Task<IActionResult> UpdateJobSchedule(Guid jobDefinitionId, Guid jobScheduleId, [FromBody] UpsertJobScheduleRequest request, CancellationToken cancellationToken)
    {
        var id = await _automationJobService.UpsertJobScheduleAsync(jobScheduleId, request with { JobDefinitionId = jobDefinitionId }, cancellationToken);
        return Ok(new IdResult(id));
    }

    [HttpPatch("schedules/{jobScheduleId:guid}/enabled")]
    public async Task<IActionResult> SetJobScheduleEnabled(Guid jobScheduleId, [FromBody] SetJobScheduleEnabledRequest request, CancellationToken cancellationToken)
    {
        await _automationJobService.SetJobScheduleEnabledAsync(jobScheduleId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{jobDefinitionId:guid}/runs")]
    public async Task<IActionResult> TriggerJobRun(Guid jobDefinitionId, [FromBody] TriggerJobRunRequest request, CancellationToken cancellationToken)
    {
        var id = await _automationJobService.TriggerJobRunAsync(jobDefinitionId, request, cancellationToken);
        return Ok(new IdResult(id));
    }

    [HttpGet("runs")]
    public async Task<IActionResult> SearchJobRuns(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid? jobDefinitionId,
        [FromQuery] string? statusCode,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(await _automationJobService.SearchJobRunsAsync(tenantId, jobDefinitionId, statusCode, pageNumber, pageSize, cancellationToken));

    [HttpGet("runs/{jobRunId:guid}/steps")]
    public async Task<IActionResult> GetJobStepRuns(Guid jobRunId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetJobStepRunsAsync(jobRunId, cancellationToken));

    [HttpGet("runs/{jobRunId:guid}/files")]
    public async Task<IActionResult> GetFileSaves(Guid jobRunId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetFileSavesAsync(jobRunId, cancellationToken));

    [HttpGet("runs/{jobRunId:guid}/file-execution-logs")]
    public async Task<IActionResult> GetFileExecutionLogs(Guid jobRunId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetFileExecutionLogsAsync(jobRunId, cancellationToken));

    [HttpGet("runs/{jobRunId:guid}/file-run-logs")]
    public async Task<IActionResult> GetFileRunLogs(Guid jobRunId, CancellationToken cancellationToken)
        => Ok(await _automationJobService.GetFileRunLogsAsync(jobRunId, cancellationToken));
}
