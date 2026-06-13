using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Common.Dtos;

public sealed class ProducerWorkbenchDto
{
    // ── Goal / KPI metrics ─────────────────────────────────────────
    public WorkbenchGoalDto Goal { get; set; } = new();
    public WorkbenchKpiCountsDto Counts { get; set; } = new();

    // ── Queue data ─────────────────────────────────────────────────
    public List<WorkbenchLeadDto>         MyLeads          { get; set; } = [];
    public List<WorkbenchOpportunityDto>  MyOpportunities  { get; set; } = [];
    public List<WorkbenchQuoteFollowupDto> QuoteFollowups  { get; set; } = [];
    public List<WorkbenchRenewalDto>       RenewalCallList { get; set; } = [];
    public List<WorkbenchCrossSellDto>     CrossSellList   { get; set; } = [];
    public List<WorkbenchNotificationDto>  Messages        { get; set; } = [];
}

public sealed class ProducerWorkbenchLogContactRequest
{
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }

    [Required, StringLength(50)]
    public string ItemType { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ContactMethod { get; set; } = "Call";

    [Required, StringLength(200)]
    public string Subject { get; set; } = "Producer workbench contact";

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(100)]
    public string? OutcomeCode { get; set; }

    [Range(0, 1440)]
    public int? DurationMinutes { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class ProducerRenewalCallListDto
{
    public ProducerRenewalCallSummaryDto Summary { get; set; } = new();
    public List<ProducerRenewalCallDto> Items { get; set; } = [];
}

public sealed class ProducerRenewalCallSummaryDto
{
    public int TotalCalls { get; set; }
    public int DueToday { get; set; }
    public int Overdue { get; set; }
    public int HighPriority { get; set; }
    public int Completed { get; set; }
    public decimal PremiumAtRisk { get; set; }
}

public sealed class ProducerRenewalCallDto
{
    public Guid RenewalCallId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AgreementRenewalId { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AssignedProducerUserId { get; set; }
    public string CallNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public string? LineOfBusiness { get; set; }
    public decimal? CurrentPremium { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime DueDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string? OutcomeCode { get; set; }
    public DateTime? LastContactDateUtc { get; set; }
    public string? NextAction { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public int DaysUntilDue => (int)(DueDate.Date - DateTime.Today).TotalDays;
    public int DaysUntilExpiration => (int)(ExpirationDate.Date - DateTime.Today).TotalDays;
    public bool IsOverdue => StatusCode != "Completed" && DueDate.Date < DateTime.Today;
    public bool IsDueToday => StatusCode != "Completed" && DueDate.Date == DateTime.Today;
    public bool IsHighPriority => PriorityCode.Equals("High", StringComparison.OrdinalIgnoreCase);
}

public sealed class UpdateProducerRenewalCallRequest
{
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Open";

    [Required, StringLength(50)]
    public string PriorityCode { get; set; } = "Medium";

    [StringLength(100)]
    public string? OutcomeCode { get; set; }

    [Required, StringLength(250)]
    public string NextAction { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public DateTime? LastContactDateUtc { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

// ── Goal ────────────────────────────────────────────────────────────────────
public sealed class WorkbenchGoalDto
{
    public decimal WrittenPremium  { get; set; }
    public decimal GoalPremium     { get; set; } = 1;
    public double  WrittenPct      => GoalPremium == 0 ? 0 : (double)(WrittenPremium / GoalPremium * 100);
    public int     NewPolicies     { get; set; }
    public double  RetentionRate   { get; set; }
    public decimal PipelineValue   { get; set; }
    public int     UnreadMessages  { get; set; }
}

// ── KPI counts ───────────────────────────────────────────────────────────────
public sealed class WorkbenchKpiCountsDto
{
    public int     AssignedLeads      { get; set; }
    public int     HotLeads           { get; set; }
    public int     OpenOpportunities  { get; set; }
    public decimal OppsPremium        { get; set; }
    public int     QuoteFollowups     { get; set; }
    public int     OverdueQuotes      { get; set; }
    public int     RenewalCallList    { get; set; }
    public int     RenewalsThisMonth  { get; set; }
    public int     CrossSellList      { get; set; }
    public decimal CrossSellPremium   { get; set; }
    public int     UnreadMessages     { get; set; }
}

// ── Leads ────────────────────────────────────────────────────────────────────
public sealed class WorkbenchLeadDto
{
    public Guid    LeadId          { get; set; }
    public string  LeadNumber      { get; set; } = string.Empty;
    public string  FirstName       { get; set; } = string.Empty;
    public string  LastName        { get; set; } = string.Empty;
    public string  FullName        => $"{FirstName} {LastName}".Trim();
    public string? AccountName     { get; set; }
    public string? Email           { get; set; }
    public string? Phone           { get; set; }
    public string? InterestedService { get; set; }
    public int?    Score           { get; set; }
    public string? PriorityCode    { get; set; }
    public string? SourceCode      { get; set; }
    public int     StatusCodeId    { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public string? NextAction      { get; set; }
    public DateTime? NextActionDate { get; set; }
    public int DaysSinceActivity   => LastActivityDate.HasValue
        ? (int)(DateTime.UtcNow - LastActivityDate.Value).TotalDays
        : (int)(DateTime.UtcNow - CreatedDateUtc).TotalDays;
}

// ── Opportunities ─────────────────────────────────────────────────────────────
public sealed class WorkbenchOpportunityDto
{
    public Guid    OpportunityId       { get; set; }
    public Guid?   AccountId           { get; set; }
    public string  OpportunityNumber   { get; set; } = string.Empty;
    public string  OpportunityName     { get; set; } = string.Empty;
    public string? AccountName         { get; set; }
    public decimal EstimatedAmount     { get; set; }
    public int     WinProbability      { get; set; }
    public string? StageName           { get; set; }
    public string? ForecastCategoryCode { get; set; }
    public DateTime? CloseDate         { get; set; }
    public int     StatusCodeId        { get; set; }
    public DateTime? LastActivityDate  { get; set; }
    public string? NextAction          { get; set; }
    public bool IsClosingSoon          => CloseDate.HasValue && CloseDate.Value <= DateTime.UtcNow.AddDays(14);
}

// ── Quote Follow-ups ─────────────────────────────────────────────────────────
public sealed class WorkbenchQuoteFollowupDto
{
    public Guid      QuoteId          { get; set; }
    public Guid?     AccountId        { get; set; }
    public Guid?     OpportunityId    { get; set; }
    public string    QuoteNumber      { get; set; } = string.Empty;
    public string?   AccountName      { get; set; }
    public string?   OpportunityName  { get; set; }
    public decimal   TotalAmount      { get; set; }
    public DateTime? ValidUntilDate   { get; set; }
    public string    StatusCode       { get; set; } = string.Empty;
    public DateTime  CreatedDateUtc   { get; set; }
    public bool IsExpired             => ValidUntilDate.HasValue && ValidUntilDate.Value.Date < DateTime.Today;
    public bool IsExpiringSoon        => ValidUntilDate.HasValue && !IsExpired && ValidUntilDate.Value.Date <= DateTime.Today.AddDays(7);
}

// ── Renewals ─────────────────────────────────────────────────────────────────
public sealed class WorkbenchRenewalDto
{
    public Guid      RenewalId          { get; set; }
    public Guid?     AccountId          { get; set; }
    public string    RenewalNumber      { get; set; } = string.Empty;
    public Guid      AgreementId        { get; set; }
    public string?   AccountName        { get; set; }
    public string?   AgreementNumber    { get; set; }
    public decimal?  TotalContractValue { get; set; }
    public DateTime  NewStartDate       { get; set; }
    public DateTime? NewEndDate         { get; set; }
    public string    StatusCode         { get; set; } = string.Empty;
    public DateTime  CreatedDateUtc     { get; set; }
    public int DaysTillRenewal          => (int)(NewStartDate.Date - DateTime.Today).TotalDays;
    public bool IsUrgent                => DaysTillRenewal <= 30;
}

// ── Cross-sell ───────────────────────────────────────────────────────────────
public sealed class WorkbenchCrossSellDto
{
    public Guid    AccountId     { get; set; }
    public string  AccountName   { get; set; } = string.Empty;
    public string? CurrentLobs   { get; set; }
    public string? TargetLob     { get; set; }
    public decimal OppPremium    { get; set; }
    public double  Score         { get; set; }
    public string? Reason        { get; set; }
    public DateTime? LastContact { get; set; }
}

// ── Notifications / Messages ──────────────────────────────────────────────────
public sealed class WorkbenchNotificationDto
{
    public Guid     NotificationId { get; set; }
    public string?  Subject        { get; set; }
    public string   Body           { get; set; } = string.Empty;
    public string   ChannelCode    { get; set; } = "InApp";
    public string?  EntityName     { get; set; }
    public Guid?    EntityId       { get; set; }
    public bool     IsRead         { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

// ── Legacy KPI DTO kept for backward compat with Dashboard ───────────────────
public sealed class ProducerTaskItemDto
{
    public Guid     ActivityId       { get; set; }
    public string   Subject          { get; set; } = string.Empty;
    public string   ActivityTypeCode { get; set; } = string.Empty;
    public string?  RelatedName      { get; set; }
    public DateTime DueDate          { get; set; }
    public bool     IsOverdue        => DueDate < DateTime.UtcNow;
}

public sealed class ProducerActivityItemDto
{
    public Guid     ActivityId       { get; set; }
    public string   Subject          { get; set; } = string.Empty;
    public string   ActivityTypeCode { get; set; } = string.Empty;
    public string?  RelatedName      { get; set; }
    public DateTime ActivityDate     { get; set; }
    public string?  OutcomeCode      { get; set; }
}
