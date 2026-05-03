namespace Ams.Application.Common.Dtos;

public sealed class MarketingConfigItemDto
{
    public Guid     MarketingConfigItemId { get; set; }
    public Guid     TenantId              { get; set; }
    public string   Kind                  { get; set; } = string.Empty;
    public string   Code                  { get; set; } = string.Empty;
    public string   Name                  { get; set; } = string.Empty;
    public string?  Category              { get; set; }
    public string?  Description           { get; set; }
    public string?  ConfigurationJson     { get; set; }
    public bool     IsActive              { get; set; }
    public int      SortOrder             { get; set; }
    public DateTime CreatedDateUtc        { get; set; }
}
