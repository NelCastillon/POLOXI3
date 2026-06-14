using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.AutomationJobs;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Automation;

public sealed class AutomationJobOrchestrator
{
    private readonly IAutomationJobRepository _jobRepository;
    private readonly IAutomationRuntimeRepository _runtimeRepository;
    private readonly IJobStepExecutorRegistry _executorRegistry;
    private readonly ILogger<AutomationJobOrchestrator> _logger;

    public AutomationJobOrchestrator(
        IAutomationJobRepository jobRepository,
        IAutomationRuntimeRepository runtimeRepository,
        IJobStepExecutorRegistry executorRegistry,
        ILogger<AutomationJobOrchestrator> logger)
    {
        _jobRepository = jobRepository;
        _runtimeRepository = runtimeRepository;
        _executorRegistry = executorRegistry;
        _logger = logger;
    }

    public async Task EnqueueDueSchedulesAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
    {
        var dueSchedules = await _runtimeRepository.GetDueSchedulesAsync(nowUtc, take, cancellationToken);
        foreach (var schedule in dueSchedules)
        {
            var correlationId = $"SCHED-{schedule.JobScheduleId:N}-{nowUtc:yyyyMMddHHmmss}";
            await _runtimeRepository.CreateScheduledJobRunAsync(new CreateScheduledJobRunRequest(
                schedule.TenantId,
                schedule.JobDefinitionId,
                schedule.JobScheduleId,
                correlationId,
                $"{{\"scheduleId\":\"{schedule.JobScheduleId}\",\"scheduledAtUtc\":\"{nowUtc:O}\"}}"), cancellationToken);

            await _runtimeRepository.UpdateJobScheduleRunStateAsync(schedule.JobScheduleId, new UpdateJobScheduleRunStateRequest(
                nowUtc,
                CronScheduleCalculator.GetNextOccurrenceUtc(schedule.CronExpression, nowUtc)), cancellationToken);
        }
    }

    public async Task ExecuteQueuedRunsAsync(int take, CancellationToken cancellationToken = default)
    {
        var queuedRuns = await _runtimeRepository.GetQueuedJobRunsAsync(take, cancellationToken);
        foreach (var run in queuedRuns)
        {
            await ExecuteRunAsync(run, cancellationToken);
        }
    }

    private async Task ExecuteRunAsync(Ams.Application.Common.Dtos.JobRunDto run, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        if (!await _runtimeRepository.TryStartJobRunAsync(run.JobRunId, new StartJobRunRequest(started), cancellationToken))
        {
            return;
        }

        var successfulSteps = 0;
        var failedSteps = 0;
        string? jobError = null;
        var finalStatus = "Completed";

        try
        {
            var job = await _jobRepository.GetJobDefinitionAsync(run.JobDefinitionId, cancellationToken);
            if (job is null)
            {
                throw new InvalidOperationException($"Job definition {run.JobDefinitionId} was not found.");
            }

            var steps = await _runtimeRepository.GetEnabledJobStepsAsync(run.JobDefinitionId, cancellationToken);
            foreach (var step in steps)
            {
                var stepRunId = await _runtimeRepository.CreateJobStepRunAsync(new CreateJobStepRunRequest(
                    step.TenantId,
                    run.JobRunId,
                    step.JobStepId,
                    step.StepOrder,
                    step.StepExecutorType,
                    step.InputMappingJson), cancellationToken);

                await _runtimeRepository.StartJobStepRunAsync(stepRunId, DateTime.UtcNow, cancellationToken);
                var result = await ExecuteStepAsync(job, run, step, stepRunId, cancellationToken);

                var stepStatus = result.Succeeded
                    ? result.CompletedWithWarnings ? "CompletedWithWarnings" : "Completed"
                    : "Failed";

                await _runtimeRepository.CompleteJobStepRunAsync(stepRunId, new CompleteJobStepRunRequest(
                    stepStatus,
                    result.OutputJson,
                    result.ErrorMessage,
                    DateTime.UtcNow), cancellationToken);

                if (result.Succeeded)
                {
                    successfulSteps++;
                    if (result.CompletedWithWarnings && finalStatus == "Completed")
                    {
                        finalStatus = "CompletedWithWarnings";
                    }

                    continue;
                }

                failedSteps++;
                jobError = result.ErrorMessage;
                await _runtimeRepository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
                    run.TenantId,
                    null,
                    run.JobRunId,
                    stepRunId,
                    "Error",
                    "StepFailed",
                    result.ErrorMessage ?? $"Step {step.StepCode} failed.",
                    null,
                    null,
                    result.OutputJson), cancellationToken);

                if (!step.ContinueOnError)
                {
                    finalStatus = "Failed";
                    break;
                }

                finalStatus = "CompletedWithWarnings";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation job run {JobRunId} failed.", run.JobRunId);
            failedSteps++;
            finalStatus = "Failed";
            jobError = ex.Message;

            await _runtimeRepository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
                run.TenantId,
                null,
                run.JobRunId,
                null,
                "Error",
                "JobRunFailed",
                $"Job run failed: {ex.Message}",
                ex.GetType().Name,
                ex.ToString(),
                run.ExecutionContextJson), cancellationToken);
        }

        await _runtimeRepository.CompleteJobRunAsync(run.JobRunId, new CompleteJobRunRequest(
            finalStatus,
            successfulSteps,
            failedSteps,
            jobError,
            DateTime.UtcNow), cancellationToken);
    }

    private async Task<JobStepExecutionResult> ExecuteStepAsync(Ams.Application.Common.Dtos.JobDefinitionDto job, Ams.Application.Common.Dtos.JobRunDto run, Ams.Application.Common.Dtos.JobStepDto step, Guid stepRunId, CancellationToken cancellationToken)
    {
        try
        {
            var executor = _executorRegistry.Resolve(step.StepExecutorType);
            return await executor.ExecuteAsync(new JobStepExecutionContext
            {
                Job = job,
                JobRun = run,
                Step = step,
                JobStepRunId = stepRunId,
                ExecutionContextJson = run.ExecutionContextJson,
                InputJson = step.InputMappingJson
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation step {StepCode} failed for run {JobRunId}.", step.StepCode, run.JobRunId);
            return JobStepExecutionResult.Failure(ex.Message, $"{{\"exceptionType\":\"{ex.GetType().Name}\"}}");
        }
    }
}
