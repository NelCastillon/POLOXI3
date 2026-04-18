namespace Ams.Application.Features.Governance;

public sealed class SubmitReviewDecisionRequest
{
    /// <summary>Keep | Remove | Escalate</summary>
    public string  DecisionCode     { get; set; } = string.Empty;
    public string? ReviewerNotes    { get; set; }
    public Guid    ReviewedByUserId { get; set; }
}
