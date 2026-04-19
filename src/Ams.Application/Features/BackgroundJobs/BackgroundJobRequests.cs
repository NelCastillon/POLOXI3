namespace Ams.Application.Features.BackgroundJobs;

public sealed class RetryBackgroundJobRequest
{
    public string? Notes { get; set; }
}

public sealed class CancelBackgroundJobRequest
{
    public string? Notes { get; set; }
}

public sealed class RequeueBackgroundJobRequest
{
    public string? Notes { get; set; }
}
