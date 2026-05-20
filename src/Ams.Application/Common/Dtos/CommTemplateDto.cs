namespace Ams.Application.Common.Dtos;

public sealed class CommTemplateDto
{
    public Guid     TemplateId          { get; init; }
    public Guid     TenantId            { get; init; }
    public string   Name                { get; init; } = string.Empty;
    public string   Channel             { get; init; } = string.Empty;
    public string   Category            { get; init; } = string.Empty;
    public string   Language            { get; init; } = "English";
    public string   Status              { get; init; } = "Active";
    public string?  Subject             { get; init; }
    public string   Body                { get; init; } = string.Empty;
    public bool     IncludeOptOutFooter { get; init; }
    public bool     TcpaNotice          { get; init; }
    public int      UsageCount          { get; init; }
    public string   ApprovalStatus      { get; init; } = "Approved";
    public string   ApprovedBy          { get; init; } = string.Empty;
    public DateTime? ApprovedDateUtc    { get; init; }
    public string   ComplianceStatus    { get; init; } = "Clear";
    public string   OwnerTeam           { get; init; } = "Communications";
    public string   SourceSystem        { get; init; } = "AMS";
    public int      VersionNumber       { get; init; } = 1;
    public DateTime LastSyncedDateUtc   { get; init; }
    public DateTime CreatedDateUtc      { get; init; }
    public DateTime UpdatedAt           { get; init; }
}
