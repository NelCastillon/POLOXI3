namespace Ams.Worker.Automation;

public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int MaxDueSchedulesPerPoll { get; set; } = 10;
    public int MaxQueuedRunsPerPoll { get; set; } = 5;
    public int RunStaleAfterMinutes { get; set; } = 120;
}
