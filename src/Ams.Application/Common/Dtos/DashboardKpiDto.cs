namespace Ams.Application.Common.Dtos;

public sealed class DashboardKpiDto
{
    public int TotalTenants { get; set; }
    public int TotalUsers { get; set; }
    public int OpenLeads { get; set; }
    public int ActiveAccounts { get; set; }
    public int OpenOpportunities { get; set; }
    public int ActiveEngagements { get; set; }
    public decimal OutstandingInvoicesAmount { get; set; }
    public decimal CollectedThisMonthAmount { get; set; }
    public int PendingApprovals { get; set; }
    public int OpenIssues { get; set; }
    public decimal PendingCommissionsAmount { get; set; }
    public int TotalDocuments { get; set; }
}
