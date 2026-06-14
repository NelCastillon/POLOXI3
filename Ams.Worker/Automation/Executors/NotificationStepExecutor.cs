using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.AutomationJobs;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Automation.Executors;

public sealed class NotificationStepExecutor : IJobStepExecutor
{
    private readonly IAutomationRuntimeRepository _repository;
    private readonly ILogger<NotificationStepExecutor> _logger;

    public NotificationStepExecutor(IAutomationRuntimeRepository repository, ILogger<NotificationStepExecutor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public string ExecutorType => nameof(NotificationStepExecutor);

    public async Task<JobStepExecutionResult> ExecuteAsync(JobStepExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing notification/audit step {StepCode} for job run {JobRunId}.", context.Step.StepCode, context.JobRun.JobRunId);

        await _repository.CreateFileExecutionLogAsync(new CreateFileExecutionLogRequest(
            context.Job.TenantId,
            null,
            context.JobRun.JobRunId,
            context.JobStepRunId,
            "Info",
            "AutomationNotificationAuditStarted",
            "Automation notification and audit step started. Notification adapters can publish operational summaries, exception review tasks, and audit events.",
            null,
            null,
            context.InputJson), cancellationToken);

        return JobStepExecutionResult.Success("{\"stage\":\"NotifyAndAudit\",\"notificationAdaptersReady\":true}");
    }
}
