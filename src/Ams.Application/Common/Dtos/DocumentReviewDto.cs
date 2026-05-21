namespace Ams.Application.Common.Dtos;

public sealed record DocumentReviewDto
{
    public Guid ReviewId { get; init; }
    public Guid TenantId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid DocumentId { get; init; }
    public string ReviewName { get; init; } = string.Empty;
    public string ReviewType { get; init; } = string.Empty;
    public string? ReviewPurpose { get; init; }
    public Guid AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime AssignedDateUtc { get; init; }
    public string ReviewStatus { get; init; } = string.Empty;
    public DateTime? CompletedDateUtc { get; init; }
    public Guid? CompletedByUserId { get; init; }
    public string? CompletedByName { get; init; }
    public string? ReviewNotes { get; init; }
    public int? Rating { get; init; }
    public int IssuesFound { get; init; }
    public bool RecommendChanges { get; init; }
    public string? ChangesDescription { get; init; }
    public DateTime? DueDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}