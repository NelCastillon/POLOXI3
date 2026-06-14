using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.AutomationJobs;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Automation.Executors;

public sealed class CarrierDownloadStepExecutor : IJobStepExecutor
{
    private readonly IAutomationRuntimeRepository _repository;
    private readonly ILogger<CarrierDownloadStepExecutor> _logger;

    public CarrierDownloadStepExecutor(IAutomationRuntimeRepository repository, ILogger<CarrierDownloadStepExecutor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public string ExecutorType => nameof(CarrierDownloadStepExecutor);

    public async Task<JobStepExecutionResult> ExecuteAsync(JobStepExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing carrier download parse step {StepCode} for job run {JobRunId}.", context.Step.StepCode, context.JobRun.JobRunId);

        await _repository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
            context.Job.TenantId,
            null,
            context.JobRun.JobRunId,
            context.JobStepRunId,
            "Info",
            "CarrierDownloadParseStarted",
            "Carrier download parsing step started. Parser adapters can normalize AL3, CSV, JSON, API, or document payloads into Integration.CarrierDownloadBatch and Integration.CarrierDownloadItem.",
            null,
            null,
            context.InputJson), cancellationToken);

        await _repository.CreateFileRunLogAsync(new CreateFileRunLogRequest(
            context.Job.TenantId,
            context.JobRun.JobRunId,
            null,
            "CarrierDownloadParse",
            "Completed",
            0,
            0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            "{\"parserReady\":true}"), cancellationToken);

        return JobStepExecutionResult.Success("{\"stage\":\"CarrierDownloadParse\",\"parserReady\":true}");
    }
}
