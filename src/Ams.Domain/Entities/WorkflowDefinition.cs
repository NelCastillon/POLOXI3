namespace Ams.Domain.Entities;

public sealed class WorkflowDefinition
{
    public Guid WorkflowDefinitionId { get; private set; } = Guid.NewGuid();
    public Guid? TenantId { get; private set; }
    public string WorkflowCode { get; private set; } = string.Empty;
    public string WorkflowName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string TargetEntityName { get; private set; } = string.Empty;
    public string TriggerTypeCode { get; private set; } = "Manual";
    public string? StepDefinitions { get; private set; }
    public decimal? ThresholdAmount { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsSystemDefined { get; private set; }
    public int Version { get; private set; } = 1;
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }

    private WorkflowDefinition() { }

    public WorkflowDefinition(string workflowCode, string workflowName, string targetEntityName, string triggerTypeCode)
    {
        WorkflowCode = workflowCode;
        WorkflowName = workflowName;
        TargetEntityName = targetEntityName;
        TriggerTypeCode = triggerTypeCode;
    }
}
