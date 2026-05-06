namespace Ams.Application.Common.Dtos;

public sealed class CsrWorkbenchDto
{
    public CsrWorkbenchCountsDto Counts { get; set; } = new();
    public List<CsrWorkbenchItemDto> ServiceRequests { get; set; } = [];
    public List<CsrWorkbenchItemDto> Endorsements { get; set; } = [];
    public List<CsrWorkbenchItemDto> Certificates { get; set; } = [];
    public List<CsrWorkbenchItemDto> BillingEnquiries { get; set; } = [];
    public List<CsrWorkbenchItemDto> Complaints { get; set; } = [];
    public List<CsrWorkbenchItemDto> FollowUps { get; set; } = [];
}

public sealed class CsrWorkbenchCountsDto
{
    public int ServiceRequests { get; set; }
    public int Endorsements { get; set; }
    public int Certificates { get; set; }
    public int BillingEnquiries { get; set; }
    public int Complaints { get; set; }
    public int FollowUps { get; set; }
    public int OverdueFollowUps { get; set; }
}

public sealed class CsrWorkbenchItemDto
{
    public Guid ItemId { get; set; }
    public string QueueCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RefNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string CertHolder { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string SlaStatus { get; set; } = "On Track";
    public int EscalationLevel { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AgeDays { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public string DetailUrl { get; set; } = "/workbench/csr";
}
