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
    public DateTime CreatedDateUtc      { get; init; }
    public DateTime UpdatedAt           { get; init; }
}
