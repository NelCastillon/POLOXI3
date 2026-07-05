namespace Ams.Application.Common.Dtos;

public sealed class EnterpriseAuditSummaryDto
{
    public int TotalEvents { get; set; }
    public int UserActivityEvents { get; set; }
    public int DataChangeEvents { get; set; }
    public int SecurityEvents { get; set; }
    public int TenantEvents { get; set; }
    public int WorkflowEvents { get; set; }
    public int DocumentEvents { get; set; }
    public int ComplianceEvents { get; set; }
    public int HighSeverityEvents { get; set; }
    public int LegalHoldEvents { get; set; }
    public int SensitiveAccessEvents { get; set; }
    public int OpenAlertEvents { get; set; }
    public DateTime? LastEventUtc { get; set; }
}
