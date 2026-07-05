namespace Ams.Application.Common.Dtos;

public sealed class UserAuditTrailSummaryDto
{
    public int TotalEvents { get; set; }
    public int SuccessfulEvents { get; set; }
    public int FailedEvents { get; set; }
    public int AccessChanges { get; set; }
    public int AuthenticationEvents { get; set; }
    public int HighSeverityEvents { get; set; }
    public int UniqueUsers { get; set; }
    public DateTime? LastEventDateUtc { get; set; }
}
