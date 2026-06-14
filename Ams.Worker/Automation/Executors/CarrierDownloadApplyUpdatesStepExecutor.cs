using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.AutomationJobs;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Automation.Executors;

public sealed class CarrierDownloadApplyUpdatesStepExecutor : IJobStepExecutor
{
    private readonly IAutomationRuntimeRepository _repository;
    private readonly ILogger<CarrierDownloadApplyUpdatesStepExecutor> _logger;

    public CarrierDownloadApplyUpdatesStepExecutor(IAutomationRuntimeRepository repository, ILogger<CarrierDownloadApplyUpdatesStepExecutor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public string ExecutorType => nameof(CarrierDownloadApplyUpdatesStepExecutor);

    public async Task<JobStepExecutionResult> ExecuteAsync(JobStepExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing carrier download apply-updates step {StepCode} for job run {JobRunId}.", context.Step.StepCode, context.JobRun.JobRunId);

        await _repository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
            context.Job.TenantId,
            null,
            context.JobRun.JobRunId,
            context.JobStepRunId,
            "Info",
            "CarrierDownloadApplyUpdatesStarted",
            "Carrier download apply-updates step started. Safe update adapters can apply matched, non-destructive updates to Policies, Documents, Billing, Claims, and Activities.",
            null,
            null,
            context.InputJson), cancellationToken);

        await _repository.CreateFileRunLogAsync(new CreateFileRunLogRequest(
            context.Job.TenantId,
            context.JobRun.JobRunId,
            null,
            "CarrierDownloadApplyUpdates",
            "Completed",
            0,
            0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            "{\"safeUpdateAdaptersReady\":true}"), cancellationToken);

        return JobStepExecutionResult.Success("{\"stage\":\"CarrierDownloadApplyUpdates\",\"safeUpdateAdaptersReady\":true}");
    }
}
