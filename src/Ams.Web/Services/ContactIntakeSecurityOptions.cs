namespace Ams.Web.Services;

public sealed class ContactIntakeSecurityOptions
{
    public bool Enabled { get; set; } = true;
    public int MinFormCompletionMilliseconds { get; set; } = 3000;
    public int MaxFormCompletionMilliseconds { get; set; } = 3_600_000;
    public int PermitLimit { get; set; } = 5;
    public int WindowMinutes { get; set; } = 5;
    public int QueueLimit { get; set; } = 0;
    public int MaxUserAgentLength { get; set; } = 500;
    public int MaxOriginLength { get; set; } = 500;
}
