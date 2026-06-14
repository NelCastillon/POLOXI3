using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.AutomationJobs;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Automation.Executors;

public sealed class CarrierDownloadMatchStepExecutor : IJobStepExecutor
{
    private readonly IAutomationRuntimeRepository _repository;
    private readonly ILogger<CarrierDownloadMatchStepExecutor> _logger;

    public CarrierDownloadMatchStepExecutor(IAutomationRuntimeRepository repository, ILogger<CarrierDownloadMatchStepExecutor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public string ExecutorType => nameof(CarrierDownloadMatchStepExecutor);

    public async Task<JobStepExecutionResult> ExecuteAsync(JobStepExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing carrier download match step {StepCode} for job run {JobRunId}.", context.Step.StepCode, context.JobRun.JobRunId);

        await _repository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
            context.Job.TenantId,
            null,
            context.JobRun.JobRunId,
            context.JobStepRunId,
            "Info",
            "CarrierDownloadMatchStarted",
            "Carrier download matching step started. Matching adapters can apply deterministic and scored matching while routing ambiguous records to Integration.CarrierDownloadException.",
            null,
            null,
            context.InputJson), cancellationToken);

        await _repository.CreateFileRunLogAsync(new CreateFileRunLogRequest(
            context.Job.TenantId,
            context.JobRun.JobRunId,
            null,
            "CarrierDownloadMatch",
            "Completed",
            0,
            0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            "{\"matcherReady\":true}"), cancellationToken);

        return JobStepExecutionResult.Success("{\"stage\":\"CarrierDownloadMatch\",\"matcherReady\":true}");
    }
}
