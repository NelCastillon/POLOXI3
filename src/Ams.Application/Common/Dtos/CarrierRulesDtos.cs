namespace Ams.Application.Common.Dtos;

public sealed class MarketAccessRuleDto
{
    public Guid     MarketAccessRuleId { get; set; }
    public Guid     TenantId           { get; set; }
    public string   RuleName           { get; set; } = string.Empty;
    public string?  CarrierNaic        { get; set; }
    public string?  StateCode          { get; set; }
    public string?  LobCode            { get; set; }
    public string?  AccessLevel        { get; set; }
    public string?  Requirements       { get; set; }
    public int      Priority           { get; set; }
    public bool     IsActive           { get; set; }
    public DateTime CreatedDateUtc     { get; set; }
}

public sealed class CarrierDownloadMappingDto
{
    public Guid     DownloadMappingId { get; set; }
    public Guid     TenantId          { get; set; }
    public string   MappingCode       { get; set; } = string.Empty;
    public string?  CarrierNaic       { get; set; }
    public string?  TransactionType   { get; set; }
    public string?  SourceField       { get; set; }
    public string?  TargetField       { get; set; }
    public string?  TransformRule     { get; set; }
    public bool     IsActive          { get; set; }
    public int      SortOrder         { get; set; }
    public DateTime CreatedDateUtc    { get; set; }
}

public sealed class CarrierRuleCategoryDto
{
    public Guid     CarrierRuleCategoryId { get; set; }
    public string   RuleCategoryCode      { get; set; } = string.Empty;
    public string   DisplayName           { get; set; } = string.Empty;
    public string?  Description           { get; set; }
    public string?  IconCssClass          { get; set; }
    public int      SortOrder             { get; set; }
    public bool     IsActive              { get; set; }
}

public sealed class CarrierRuleOptionDto
{
    public Guid CarrierRuleOptionId { get; set; }
    public Guid TenantId { get; set; }
    public string OptionType { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OptionValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CarrierProductCatalogDto
{
    public Guid CarrierProductCatalogId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CarrierProductRuleDto
{
    public Guid      CarrierProductRuleId       { get; set; }
    public Guid      TenantId                   { get; set; }
    public Guid?     CarrierId                  { get; set; }
    public string?   CarrierName                { get; set; }
    public string?   CarrierNaic                { get; set; }
    public string?   CarrierProductCode         { get; set; }
    public string    CarrierProductName         { get; set; } = string.Empty;
    public Guid?     LineOfBusinessId           { get; set; }
    public string?   LineOfBusinessCode         { get; set; }
    public string?   StateCode                  { get; set; }
    public string    RuleCategoryCode           { get; set; } = string.Empty;
    public string    RuleCode                   { get; set; } = string.Empty;
    public string    RuleName                   { get; set; } = string.Empty;
    public string?   RuleDescription            { get; set; }
    public DateTime  EffectiveDate              { get; set; }
    public DateTime? ExpirationDate             { get; set; }
    public int       Priority                   { get; set; }
    public string?   BillingType                { get; set; }
    public decimal?  MinimumDownPaymentPercent  { get; set; }
    public decimal?  MinimumDownPaymentAmount   { get; set; }
    public int?      MaximumInstallments        { get; set; }
    public bool      RequirePaymentBeforeBinding { get; set; }
    public bool      AllowPremiumFinance        { get; set; }
    public bool      AllowAgencyBill            { get; set; }
    public bool      AllowDirectBill            { get; set; }
    public bool      AllowZeroDown              { get; set; }
    public bool      RequireSignedApplication   { get; set; }
    public bool      RequirePayment             { get; set; }
    public bool      RequireInspection          { get; set; }
    public bool      RequirePhotos              { get; set; }
    public bool      RequireLossRuns            { get; set; }
    public bool      AllowSameDayBind           { get; set; }
    public int?      MaximumAdvanceBindDays     { get; set; }
    public bool      AllowWeekendBinding        { get; set; }
    public TimeSpan? BindingTimeCutoff          { get; set; }
    public bool      RequireUnderwriterApproval { get; set; }
    public bool      RequireACORD125            { get; set; }
    public bool      RequireACORD126            { get; set; }
    public bool      RequireACORD127            { get; set; }
    public bool      RequireStatementOfValues   { get; set; }
    public bool      RequireFinancialStatement  { get; set; }
    public bool      RequireSupplementalForm    { get; set; }
    public decimal?  NewBusinessRate            { get; set; }
    public decimal?  RenewalRate                { get; set; }
    public bool      BrokerFeeAllowed           { get; set; }
    public decimal?  MaximumBrokerFee           { get; set; }
    public string?   CommissionSchedule         { get; set; }
    public string?   CommissionPaymentMethod    { get; set; }
    public bool      ValidateVIN                { get; set; }
    public bool      ValidateFEIN               { get; set; }
    public bool      ValidateRoofAge            { get; set; }
    public bool      ValidateDriverAge          { get; set; }
    public bool      ValidatePayroll            { get; set; }
    public bool      ValidateSquareFootage      { get; set; }
    public bool      ValidateClaimsHistory      { get; set; }
    public string    RulePayloadJson            { get; set; } = "{}";
    public string?   Notes                      { get; set; }
    public bool      IsActive                   { get; set; }
    public DateTime  CreatedDateUtc             { get; set; }
    public DateTime? ModifiedDateUtc            { get; set; }
}
