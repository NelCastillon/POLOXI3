namespace Ams.Application.Common.Dtos;

public sealed class AcknowledgementSummaryDto
{
    public int TotalPoliciesWithAudience { get; set; }
    public int TotalPending              { get; set; }
    public int TotalOverdue              { get; set; }
    public int TotalAcknowledged         { get; set; }
}
