namespace Ams.Worker.Automation;

public interface IJobStepExecutorRegistry
{
    IJobStepExecutor Resolve(string executorType);
}
