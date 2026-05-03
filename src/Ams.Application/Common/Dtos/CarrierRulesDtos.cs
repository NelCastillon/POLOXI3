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
