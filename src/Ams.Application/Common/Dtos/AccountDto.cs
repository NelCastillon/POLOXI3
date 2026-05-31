namespace Ams.Application.Common.Dtos;

public sealed class AccountDto
{
    public Guid AccountId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountTypeCode { get; set; } = string.Empty;
    public string? MainEmail { get; set; }
    public string? MainPhone { get; set; }

    // Status and Lifecycle
    public string StatusCode { get; set; } = string.Empty;
    public int StatusCodeId { get; set; }
    public string? SegmentCode { get; set; }
    public string? LifecycleStageCode { get; set; }

    // Ownership
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public Guid? ParentAccountId { get; set; }
    public string? ParentAccountName { get; set; }
    public Guid? ServicingTeamId { get; set; }
    public string? ServicingTeamName { get; set; }

    // Business Information
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? Employees { get; set; }
    public string? TaxId { get; set; }
    public string? NaicsCode { get; set; }

    // Address
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }

    // Audit
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? ModifiedByUserId { get; set; }

    // Dashboard Metrics (computed fields)
    public decimal TotalPremium { get; set; }
    public decimal BalancesDue { get; set; }
    public int ActivePolicies { get; set; }
    public int OpenClaims { get; set; }
    public int OpenOpportunities { get; set; }
    public decimal YtdCommissions { get; set; }
    public string RenewalRisk { get; set; } = "Medium"; // Low, Medium, High
    public DateTime? LastActivityDate { get; set; }
    public int EngagementScore { get; set; }
    public int PortalLogins { get; set; }
    public int DaysSinceLastTouch { get; set; }
}
