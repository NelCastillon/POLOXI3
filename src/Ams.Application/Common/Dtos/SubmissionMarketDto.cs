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
    public DateTime AddedDateUtc { get; set; }
    public DateTime? RespondedDateUtc { get; set; }
}
