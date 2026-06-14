namespace Ams.Worker.Automation;

public sealed class JobStepExecutorRegistry : IJobStepExecutorRegistry
{
    private readonly IReadOnlyDictionary<string, IJobStepExecutor> _executors;

    public JobStepExecutorRegistry(IEnumerable<IJobStepExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.ExecutorType, StringComparer.OrdinalIgnoreCase);
    }

    public IJobStepExecutor Resolve(string executorType)
    {
        if (_executors.TryGetValue(executorType, out var executor))
        {
            return executor;
        }

        throw new InvalidOperationException($"No automation step executor is registered for '{executorType}'.");
    }
}
