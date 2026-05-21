namespace Ams.Application.Common.Dtos;

public sealed record DocumentWorkflowTemplateDto
{
    public Guid WorkflowTemplateId { get; init; }
    public Guid TenantId { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string WorkflowType { get; init; } = string.Empty;
    public bool IsSequential { get; init; }
    public bool RequiresAllApprovals { get; init; }
    public bool AutoArchiveOnComplete { get; init; }
    public bool NotifyOnStart { get; init; }
    public bool NotifyOnComplete { get; init; }
    public bool TriggerOnUpload { get; init; }
    public string? TriggerOnCategory { get; init; }
    public string? TriggerOnDocType { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime? ModifiedDateUtc { get; init; }
    public Guid? ModifiedByUserId { get; init; }
}
