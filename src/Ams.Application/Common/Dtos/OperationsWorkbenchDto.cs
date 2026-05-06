namespace Ams.Application.Common.Dtos;

public sealed class OperationsWorkbenchDto
{
    public OperationsWorkbenchCountsDto Counts { get; set; } = new();
    public List<OperationsWorkbenchItemDto> OverdueTasks { get; set; } = [];
    public List<OperationsWorkbenchItemDto> PendingEndorsements { get; set; } = [];
    public List<OperationsWorkbenchItemDto> CertificateRequests { get; set; } = [];
    public List<OperationsWorkbenchItemDto> RenewalFollowups { get; set; } = [];
    public List<OperationsWorkbenchItemDto> DocExceptions { get; set; } = [];
    public List<OperationsWorkbenchItemDto> FailedDownloads { get; set; } = [];
    public List<OperationsWorkbenchItemDto> FailedAutomations { get; set; } = [];
}

public sealed class OperationsWorkbenchCountsDto
{
    public int OverdueTasks { get; set; }
    public int PendingEndorsements { get; set; }
    public int CertificateRequests { get; set; }
    public int RenewalFollowups { get; set; }
    public int DocIndexingExceptions { get; set; }
    public int FailedDownloads { get; set; }
    public int FailedAutomations { get; set; }
}

public sealed class OperationsWorkbenchItemDto
{
    public Guid ItemId { get; set; }
    public string QueueCode { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RefNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string CertHolder { get; set; } = string.Empty;
    public string LobCode { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime DueDate { get; set; } = DateTime.Today;
    public DateTime? FollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int AgeDays { get; set; }
    public decimal Premium { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? AutomationStep { get; set; }
    public string? RenewalStage { get; set; }
    public string? Notes { get; set; }
    public bool CanRetry { get; set; }
    public bool IsAssignedToMe { get; set; }
    public string DetailUrl { get; set; } = "/workbench/operations";
}
