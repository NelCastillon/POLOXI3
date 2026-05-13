namespace Ams.Application.Common.Dtos;

public sealed class ExecutiveDashboardPageDto
{
    public ExecutiveDashboardKpiDto Kpi { get; set; } = new();
    public List<DashboardPremiumPointDto> PremiumTrend { get; set; } = [];
    public List<DashboardNewBusinessPointDto> NewBusinessTrend { get; set; } = [];
    public List<DashboardPiePointDto> RetentionSegments { get; set; } = [];
    public List<DashboardRenewalRiskDto> RenewalsAtRisk { get; set; } = [];
    public List<DashboardPiePointDto> ClaimsBySeverity { get; set; } = [];
    public List<DashboardPiePointDto> ReceivablesAging { get; set; } = [];
    public List<DashboardCampaignRowDto> Campaigns { get; set; } = [];
    public List<DashboardProducerRowDto> Producers { get; set; } = [];
}

public sealed class ExecutiveDashboardKpiDto
{
    public decimal WrittenPremium { get; set; }
    public double WrittenPremiumDelta { get; set; }
    public double RetentionRate { get; set; }
    public double RetentionDelta { get; set; }
    public decimal NewBusinessPremium { get; set; }
    public double NewBusinessDelta { get; set; }
    public int RenewalAtRiskCount { get; set; }
    public decimal RenewalAtRiskPremium { get; set; }
    public int OpenClaimsCount { get; set; }
    public decimal TotalIncurredLoss { get; set; }
    public decimal OverdueReceivables { get; set; }
    public double OverduePct { get; set; }
}

public sealed class DashboardPremiumPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal PriorValue { get; set; }
}

public sealed class DashboardNewBusinessPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal NewBiz { get; set; }
    public decimal Renewal { get; set; }
}

public sealed class DashboardPiePointDto
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public sealed class DashboardRenewalRiskDto
{
    public string AccountName { get; set; } = string.Empty;
    public string LobCode { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Premium { get; set; }
    public string RiskLevel { get; set; } = "Low";
}

public sealed class DashboardCampaignRowDto
{
    public string Name { get; set; } = string.Empty;
    public int Leads { get; set; }
    public int Quoted { get; set; }
    public int Bound { get; set; }
    public double ConversionPct { get; set; }
    public decimal Premium { get; set; }
}

public sealed class DashboardProducerRowDto
{
    public string ProducerName { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public decimal WrittenPremium { get; set; }
    public int PoliciesWritten { get; set; }
    public decimal NewBusiness { get; set; }
    public double RetentionRate { get; set; }
    public double GoalPct { get; set; }
    public string Lob { get; set; } = string.Empty;
}

public sealed class DashboardDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-grid";
    public string IconCss { get; set; } = string.Empty;
    public string Audience { get; set; } = "All";
    public int WidgetCount { get; set; }
    public bool IsDefault { get; set; }
    public DateTime LastEdited { get; set; }
    public List<string> Widgets { get; set; } = [];
}

public sealed class DashboardKpiDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
    public string Critical { get; set; } = string.Empty;
    public string Direction { get; set; } = "Higher is better";
    public string Frequency { get; set; } = "Monthly";
    public string Owner { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed record UpsertDashboardRecordRequest(Guid TenantId, string Kind, string Code, string Name, string Status, string JsonData);
