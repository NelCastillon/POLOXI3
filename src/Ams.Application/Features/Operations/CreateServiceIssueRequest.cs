namespace Ams.Application.Features.Operations;

public sealed class CreateServiceIssueRequest
{
    public Guid TenantId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? EngagementId { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SeverityCode { get; set; } = "Medium";
    public Guid? AssignedToUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
