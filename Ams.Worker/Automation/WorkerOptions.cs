namespace Ams.Worker.Automation;

public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int MaxDueSchedulesPerPoll { get; set; } = 10;
    public int MaxQueuedRunsPerPoll { get; set; } = 5;
    public int RunStaleAfterMinutes { get; set; } = 120;
    public int PaymentPollIntervalSeconds { get; set; } = 60;
    public int MaxPaymentRetriesPerPoll { get; set; } = 50;
    public int MaxPaymentSettlementCredentialsPerPoll { get; set; } = 25;
    public int SubmitToMarketPollIntervalSeconds { get; set; } = 30;
    public int MaxSubmitToMarketDispatchesPerPoll { get; set; } = 25;
    public int QuoteRequestFollowUpPollIntervalSeconds { get; set; } = 300;
}
