namespace Ams.Worker.Automation;

public interface IJobStepExecutor
{
    string ExecutorType { get; }
    Task<JobStepExecutionResult> ExecuteAsync(JobStepExecutionContext context, CancellationToken cancellationToken = default);
}
