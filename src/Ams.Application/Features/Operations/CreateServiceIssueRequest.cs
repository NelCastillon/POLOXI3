using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateServiceIssueRequest
{
    public Guid TenantId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? EngagementId { get; set; }
    [Required, StringLength(50)]
    public string IssueNumber { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Description { get; set; }
    [Required, StringLength(50)]
    public string SeverityCode { get; set; } = "Medium";
    public Guid? AssignedToUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
