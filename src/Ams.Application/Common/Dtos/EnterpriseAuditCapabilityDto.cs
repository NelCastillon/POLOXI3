namespace Ams.Application.Common.Dtos;

public sealed class EnterpriseAuditCapabilityDto
{
    public Guid AuditCapabilityId { get; set; }
    public string CapabilityArea { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public bool IsImplemented { get; set; }
    public bool IsSeeded { get; set; }
    public bool RequiresInstrumentation { get; set; }
    public int DisplayOrder { get; set; }
}
