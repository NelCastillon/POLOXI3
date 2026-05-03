namespace Ams.Application.Common.Dtos;

// ── Executive Overview ───────────────────────────────────────────────────────
public sealed class AgencyExecutiveOverviewDto
{
    // Agency identity
    public string  AgencyName        { get; set; } = string.Empty;
    public string? AgencyCode        { get; set; }

    // Top-line KPIs
    public decimal WrittenPremiumMtd  { get; set; }
    public decimal WrittenPremiumYtd  { get; set; }
    public decimal WrittenPremiumGoal { get; set; }
    public decimal RetentionRate      { get; set; }   // 0-100 %
    public decimal ConversionRate     { get; set; }   // 0-100 %
    public int     ActivePolicies     { get; set; }
    public int     ActiveAccounts     { get; set; }
    public int     OpenLeads          { get; set; }
    public int     OpenOpportunities  { get; set; }
    public int     OpenClaims         { get; set; }
    public int     PendingRenewals    { get; set; }
    public decimal OutstandingAr      { get; set; }
    public int     OpenAlerts         { get; set; }

    // Trend rows (last 6 months)
    public List<MonthlyTrendDto> PremiumTrend { get; set; } = [];
}

// ── Agency KPIs ──────────────────────────────────────────────────────────────
public sealed class AgencyKpiDto
{
    public decimal WrittenPremiumMtd    { get; set; }
    public decimal WrittenPremiumYtd    { get; set; }
    public decimal WrittenPremiumPriorYtd { get; set; }
    public decimal RetentionRate        { get; set; }
    public decimal NewBusinessRate      { get; set; }
    public int     TotalActivePolicies  { get; set; }
    public int     NewPoliciesMtd       { get; set; }
    public int     CancelledPoliciesMtd { get; set; }
    public int     ActiveProducers      { get; set; }
    public decimal AvgPremiumPerPolicy  { get; set; }
    public int     LeadsThisMonth       { get; set; }
    public decimal LeadConversionRate   { get; set; }
    public int     QuotesMtd            { get; set; }
    public decimal QuoteConversionRate  { get; set; }
}

// ── Branch Performance ───────────────────────────────────────────────────────
public sealed class BranchPerformanceDto
{
    public Guid    BranchId       { get; set; }
    public string  BranchName     { get; set; } = string.Empty;
    public string? BranchCode     { get; set; }
    public string? City           { get; set; }
    public string? StateProvince  { get; set; }
    public decimal WrittenPremiumMtd  { get; set; }
    public decimal WrittenPremiumYtd  { get; set; }
    public int     ActivePolicies     { get; set; }
    public int     ActiveProducers    { get; set; }
    public decimal RetentionRate      { get; set; }
    public int     OpenLeads          { get; set; }
    public int     OpenClaims         { get; set; }
    public decimal OutstandingAr      { get; set; }
}

// ── Producer Performance ─────────────────────────────────────────────────────
public sealed class ProducerPerformanceDto
{
    public Guid    UserId             { get; set; }
    public string  DisplayName        { get; set; } = string.Empty;
    public string? Email              { get; set; }
    public string? BranchName         { get; set; }
    public decimal WrittenPremiumMtd  { get; set; }
    public decimal WrittenPremiumYtd  { get; set; }
    public int     NewPoliciesMtd     { get; set; }
    public int     OpenLeads          { get; set; }
    public int     OpenOpportunities  { get; set; }
    public decimal RetentionRate      { get; set; }
    public int     QuotesMtd          { get; set; }
    public decimal QuoteConversionRate { get; set; }
}

// ── Renewal Pipeline ─────────────────────────────────────────────────────────
public sealed class RenewalPipelineDto
{
    // Summary buckets
    public int     DueIn30Days        { get; set; }
    public decimal PremiumDueIn30Days { get; set; }
    public int     DueIn60Days        { get; set; }
    public decimal PremiumDueIn60Days { get; set; }
    public int     DueIn90Days        { get; set; }
    public decimal PremiumDueIn90Days { get; set; }
    public int     Overdue            { get; set; }
    public decimal PremiumOverdue     { get; set; }

    // Row-level detail
    public List<RenewalPipelineRowDto> Rows { get; set; } = [];
}

public sealed class RenewalPipelineRowDto
{
    public Guid    AgreementRenewalId { get; set; }
    public string  PolicyNumber       { get; set; } = string.Empty;
    public string  AccountName        { get; set; } = string.Empty;
    public string? ProducerName       { get; set; }
    public string? BranchName         { get; set; }
    public DateTime RenewalDate       { get; set; }
    public decimal  CurrentPremium    { get; set; }
    public string   StatusCode        { get; set; } = string.Empty;
    public int      DaysUntilRenewal  { get; set; }
}

// ── Claims Summary ───────────────────────────────────────────────────────────
public sealed class ClaimsSummaryDto
{
    public int     TotalOpenClaims       { get; set; }
    public int     NewClaimsMtd          { get; set; }
    public int     ClosedClaimsMtd       { get; set; }
    public decimal TotalReservedAmount   { get; set; }
    public decimal TotalPaidMtd          { get; set; }
    public int     LitigatedClaims       { get; set; }
    public double  AvgDaysToClose        { get; set; }

    public List<ClaimsByStatusDto>  ByStatus   { get; set; } = [];
    public List<ClaimsByLobDto>     ByLob      { get; set; } = [];
}

public sealed class ClaimsByStatusDto
{
    public string StatusCode { get; set; } = string.Empty;
    public int    Count      { get; set; }
    public decimal Reserved  { get; set; }
}

public sealed class ClaimsByLobDto
{
    public string LobName { get; set; } = string.Empty;
    public int    Count   { get; set; }
    public decimal Reserved { get; set; }
}

// ── Billing Summary ──────────────────────────────────────────────────────────
public sealed class BillingSummaryDto
{
    public decimal OutstandingArTotal    { get; set; }
    public decimal CollectedMtd          { get; set; }
    public decimal OverdueBalance        { get; set; }
    public int     OverdueInvoiceCount   { get; set; }
    public int     TotalOpenInvoices     { get; set; }
    public decimal PendingCommissions    { get; set; }
    public decimal PaidCommissionsMtd    { get; set; }

    public List<ArAgingBucketDto> ArAging { get; set; } = [];
}

public sealed class ArAgingBucketDto
{
    public string  BucketLabel { get; set; } = string.Empty;   // "Current", "1-30", "31-60", "61-90", "90+"
    public decimal Amount      { get; set; }
    public int     InvoiceCount { get; set; }
}

// ── Shared ───────────────────────────────────────────────────────────────────
public sealed class MonthlyTrendDto
{
    public int     Year    { get; set; }
    public int     Month   { get; set; }
    public string  Label   { get; set; } = string.Empty;   // e.g. "Jan 25"
    public decimal Amount  { get; set; }
    public int     Count   { get; set; }
}
