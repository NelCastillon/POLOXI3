namespace Ams.Application.Common.Dtos;

public sealed class SodConflictDto
{
    public Guid     SodConflictId      { get; set; }
    public Guid     TenantId           { get; set; }
    // Linked rule
    public Guid     SodRuleId          { get; set; }
    public string   RuleCode           { get; set; } = string.Empty;
    public string   RuleName           { get; set; } = string.Empty;
    public string   SeverityCode       { get; set; } = string.Empty;
    // Conflicting user
    public Guid     UserId             { get; set; }
    public string   UserFullName       { get; set; } = string.Empty;
    public string   UserEmail          { get; set; } = string.Empty;
    // Lifecycle
    public DateTime DetectedDateUtc    { get; set; }
    public string   StatusCode         { get; set; } = "Open"; // Open | InReview | Remediated | Resolved
    public bool     IsResolved         { get; set; }
    // Reviewer
    public Guid?    ReviewerUserId     { get; set; }
    public string?  ReviewerFullName   { get; set; }
    // Remediation
    public string?  RemediationNote    { get; set; }
    // Resolution
    public Guid?    ResolvedByUserId   { get; set; }
    public string?  ResolvedByFullName { get; set; }
    public string?  ResolutionNote     { get; set; }
    public DateTime? ResolvedDateUtc   { get; set; }
    // Audit
    public DateTime  CreatedDateUtc    { get; set; }
    public DateTime? ModifiedDateUtc   { get; set; }
}
