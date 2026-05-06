namespace Ams.Application.Common.Dtos;

public sealed class ServiceManagerWorkbenchDto
{
    public ServiceManagerWorkbenchCountsDto Counts { get; set; } = new();
    public List<ServiceManagerWorkbenchItemDto> Escalations { get; set; } = [];
    public List<ServiceManagerWorkbenchItemDto> SlaBreaches { get; set; } = [];
    public List<ServiceManagerAgentCapacityDto> AgentCapacity { get; set; } = [];
    public List<ServiceManagerWorkbenchItemDto> QualityAudits { get; set; } = [];
    public List<ServiceManagerWorkbenchItemDto> CarrierTickets { get; set; } = [];
    public List<ServiceManagerWorkbenchItemDto> Unassigned { get; set; } = [];
}

public sealed class ServiceManagerWorkbenchCountsDto
{
    public int Escalations { get; set; }
    public int SlaBreaches { get; set; }
    public int AgentsOnline { get; set; }
    public int AgentsTotal { get; set; }
    public double TeamCapacityPct { get; set; }
    public int QualityAudits { get; set; }
    public double AvgQualityScore { get; set; }
    public int CarrierTickets { get; set; }
    public int Unassigned { get; set; }
}

public sealed class ServiceManagerWorkbenchItemDto
{
    public Guid ItemId { get; set; }
    public string QueueCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RefNumber { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string EscalatedBy { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string AuditedBy { get; set; } = string.Empty;
    public string QualityNotes { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string SlaStatus { get; set; } = "On Track";
    public int Level { get; set; } = 1;
    public int AgeDays { get; set; }
    public int SlaBreachMins { get; set; }
    public double QualityScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AuditedAt { get; set; }
    public string? Notes { get; set; }
    public string DetailUrl { get; set; } = "/workbench/service-manager";
}

public sealed class ServiceManagerAgentCapacityDto
{
    public string AgentName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string Status { get; set; } = "Online";
    public int OpenItems { get; set; }
    public int OverdueItems { get; set; }
    public double UtilPct { get; set; }
}
