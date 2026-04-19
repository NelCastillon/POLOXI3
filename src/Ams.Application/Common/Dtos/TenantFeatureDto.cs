namespace Ams.Application.Common.Dtos;

public sealed class TenantFeatureDto
{
    public Guid      TenantFeatureId   { get; set; }
    public Guid      TenantId          { get; set; }
    public string    FeatureCode       { get; set; } = string.Empty;
    public string    FeatureName       { get; set; } = string.Empty;
    public string    Module            { get; set; } = string.Empty;
    public bool      IsEnabled         { get; set; }
    public DateTime? EffectiveStartUtc { get; set; }
    public DateTime? EffectiveEndUtc   { get; set; }
    public string    SourceType        { get; set; } = string.Empty;
    public DateTime  EnabledDateUtc    { get; set; }
    public DateTime? ModifiedDateUtc   { get; set; }
}
