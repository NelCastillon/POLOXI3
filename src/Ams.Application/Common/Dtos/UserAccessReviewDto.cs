namespace Ams.Application.Common.Dtos;

public sealed class UserAccessReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid TenantId { get; set; }
    public string ReviewCycleCode { get; set; } = string.Empty;
    public Guid ReviewerUserId { get; set; }
    public string? ReviewerFullName { get; set; }
    public Guid SubjectUserId { get; set; }
    public string? SubjectFullName { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string? DecisionNotes { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
    public DateTime DueByDateUtc { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
