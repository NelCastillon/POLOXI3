namespace Ams.Application.Common.Dtos;

public sealed class AutomationFlowDto
{
    public Guid AutomationFlowId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RunCount { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
}
