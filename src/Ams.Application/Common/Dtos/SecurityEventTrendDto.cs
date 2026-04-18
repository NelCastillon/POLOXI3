namespace Ams.Application.Common.Dtos;

public sealed class SecurityEventTrendDto
{
    public DateTime EventDate         { get; set; }
    public int      FailedLoginCount  { get; set; }
    public int      HighSeverityCount { get; set; }
    public int      TotalCount        { get; set; }
}
