namespace Ams.Application.Common.Dtos;

public sealed class LeadEngagementFactorDto
{
    public Guid EngagementFactorId { get; set; }
    public Guid TenantId { get; set; }
    public string FactorName { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class LeadEngagementFactorContributionDto
{
    public Guid EngagementFactorId { get; set; }
    public string FactorName { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool Matched { get; set; }
    public decimal ActualValue { get; set; }
}

public sealed class LeadEngagementSummaryDto
{
    public int Score { get; set; }
    public string Level { get; set; } = "Low";
    public int EmailsSent { get; set; }
    public int EmailsOpened { get; set; }
    public int Clicks { get; set; }
    public int PortalVisits { get; set; }
    public int ActivityCount { get; set; }
    public int DaysSinceTouch { get; set; }
    public IReadOnlyList<LeadEngagementFactorContributionDto> Factors { get; set; } = [];
}
