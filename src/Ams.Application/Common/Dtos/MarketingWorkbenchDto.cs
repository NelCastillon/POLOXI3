namespace Ams.Application.Common.Dtos;

public sealed class MarketingWorkbenchDto
{
    public MarketingWorkbenchCountsDto Counts { get; set; } = new();
    public List<MarketingWorkbenchItemDto> Campaigns { get; set; } = [];
    public List<MarketingWorkbenchItemDto> Outreach { get; set; } = [];
    public List<MarketingWorkbenchItemDto> Referrals { get; set; } = [];
    public List<MarketingWorkbenchItemDto> Events { get; set; } = [];
    public List<MarketingWorkbenchItemDto> Content { get; set; } = [];
    public List<MarketingWorkbenchLeadSourceDto> LeadSources { get; set; } = [];
}

public sealed class MarketingWorkbenchCountsDto
{
    public int ActiveCampaigns { get; set; }
    public int CampaignLeads { get; set; }
    public int OutreachTasks { get; set; }
    public int OutreachOverdue { get; set; }
    public int Referrals { get; set; }
    public int ReferralsConverted { get; set; }
    public int UpcomingEvents { get; set; }
    public int EventFollowUps { get; set; }
    public int ContentPendingApproval { get; set; }
    public int TotalLeads { get; set; }
    public int LeadsConverted { get; set; }
    public double ConversionRate { get; set; }
    public decimal CostPerLead { get; set; }
}

public sealed class MarketingWorkbenchItemDto
{
    public Guid ItemId { get; set; }
    public string QueueCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RefNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string SlaStatus { get; set; } = "On Track";
    public string Status { get; set; } = "Active";
    public decimal Budget { get; set; }
    public decimal EstPremium { get; set; }
    public int Leads { get; set; }
    public int Conversions { get; set; }
    public int Attendees { get; set; }
    public DateTime DueDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);
    public DateTime? EventDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? Notes { get; set; }
    public string DetailUrl { get; set; } = "/workbench/marketing";
}

public sealed class MarketingWorkbenchLeadSourceDto
{
    public string SourceName { get; set; } = string.Empty;
    public int Leads { get; set; }
    public int Converted { get; set; }
    public decimal AvgPremium { get; set; }
}
