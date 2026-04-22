namespace Ams.Application.Common.Dtos;

public sealed class WorkflowDesignDto
{
    public Guid WorkflowDesignId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DiagramJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
    public string? LastModifiedByUserId { get; set; }
}
