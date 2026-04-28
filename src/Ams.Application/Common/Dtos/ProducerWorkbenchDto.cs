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
    public string  OpportunityNumber   { get; set; } = string.Empty;
    public string  OpportunityName     { get; set; } = string.Empty;
    public string? AccountName         { get; set; }
    public decimal EstimatedAmount     { get; set; }
    public int     WinProbability      { get; set; }
    public string? StageName           { get; set; }
    public string? ForecastCategoryCode { get; set; }
    public DateTime? CloseDate         { get; set; }
    public int     StatusCodeId        { get; set; }
    public string? NextAction          { get; set; }
    public bool IsClosingSoon          => CloseDate.HasValue && CloseDate.Value <= DateTime.UtcNow.AddDays(14);
}

// ── Quote Follow-ups ─────────────────────────────────────────────────────────
public sealed class WorkbenchQuoteFollowupDto
{
    public Guid      QuoteId          { get; set; }
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
