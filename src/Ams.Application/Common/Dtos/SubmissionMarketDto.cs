namespace Ams.Application.Common.Dtos;

public sealed class SubmissionMarketDto
{
    public Guid SubmissionMarketId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AppetiteScore { get; set; }
    public bool IsRecommended { get; set; }
    public string? DeclineReason { get; set; }
    public string? UnderwriterName { get; set; }
    public string? UnderwriterEmail { get; set; }
    public string? UnderwriterPhone { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public string? RequestedCoverageSummary { get; set; }
    public string? RequestedLimits { get; set; }
    public string? SubmissionMethodCode { get; set; }
    public Guid? FollowUpTaskId { get; set; }
    public Guid? LatestQuoteId { get; set; }
    public string? LatestQuoteNumber { get; set; }
    public string? LatestQuoteStatus { get; set; }
    public DateTime? LatestQuoteReceivedDateUtc { get; set; }
    public DateTime AddedDateUtc { get; set; }
    public DateTime? SubmittedDateUtc { get; set; }
    public DateTime? RespondedDateUtc { get; set; }
}
