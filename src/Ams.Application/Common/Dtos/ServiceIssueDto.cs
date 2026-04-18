namespace Ams.Application.Common.Dtos;

public sealed class ServiceIssueDto
{
    public Guid IssueId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? EngagementId { get; set; }
    public Guid? AccountId { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateOnly? ResolvedDate { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
