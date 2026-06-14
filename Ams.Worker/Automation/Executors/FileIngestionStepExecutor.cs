using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.AutomationJobs;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Automation.Executors;

public sealed class FileIngestionStepExecutor : IJobStepExecutor
{
    private readonly IAutomationRuntimeRepository _repository;
    private readonly ILogger<FileIngestionStepExecutor> _logger;

    public FileIngestionStepExecutor(IAutomationRuntimeRepository repository, ILogger<FileIngestionStepExecutor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public string ExecutorType => nameof(FileIngestionStepExecutor);

    public async Task<JobStepExecutionResult> ExecuteAsync(JobStepExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing file ingestion step {StepCode} for job run {JobRunId}.", context.Step.StepCode, context.JobRun.JobRunId);

        await _repository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
            context.Job.TenantId,
            null,
            context.JobRun.JobRunId,
            context.JobStepRunId,
            "Info",
            "FileIngestionStarted",
            "File ingestion step started. Configure source-specific adapters to save files into Automation.FileSave.",
            null,
            null,
            context.InputJson), cancellationToken);

        await _repository.CreateFileRunLogAsync(new CreateFileRunLogRequest(
            context.Job.TenantId,
            context.JobRun.JobRunId,
            null,
            "FileIngestion",
            "Completed",
            0,
            0,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            "{\"adapterReady\":true}"), cancellationToken);

        return JobStepExecutionResult.Success("{\"stage\":\"FileIngestion\",\"adapterReady\":true}");
    }
}
