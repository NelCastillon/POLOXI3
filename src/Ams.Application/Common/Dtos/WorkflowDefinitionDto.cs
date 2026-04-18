namespace Ams.Application.Common.Dtos;

public sealed class WorkflowDefinitionDto
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? TenantId { get; set; }
    public string WorkflowCode { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TargetEntityName { get; set; } = string.Empty;
    public string TriggerTypeCode { get; set; } = string.Empty;
    public decimal? ThresholdAmount { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public int Version { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
