namespace Ams.Application.Common.Dtos;

public sealed record DocumentClassificationQueueDto
{
    public Guid ClassificationQueueId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public string QueueStatus { get; init; } = string.Empty;
    public string ClassificationMethod { get; init; } = string.Empty;
    public decimal? OcrConfidence { get; init; }
    public string? SuggestedCategory { get; init; }
    public string? SuggestedDocType { get; init; }
    public string? ExtractedText { get; init; }
    public string? ExtractedMetadata { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime? AssignedDateUtc { get; init; }
    public Guid? ClassifiedByUserId { get; init; }
    public string? ClassifiedByName { get; init; }
    public DateTime? ClassifiedDateUtc { get; init; }
    public string? FinalCategory { get; init; }
    public string? FinalDocType { get; init; }
    public string? ClassificationNotes { get; init; }
    public string Priority { get; init; } = string.Empty;
    public DateTime? DueDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
