namespace Ams.Application.Common.Dtos;

public sealed class FeatureCatalogDto
{
    public Guid     FeatureId      { get; set; }
    public string   FeatureCode    { get; set; } = string.Empty;
    public string   FeatureName    { get; set; } = string.Empty;
    public string   Module         { get; set; } = string.Empty;
    public string   TypeCode       { get; set; } = string.Empty;
    public bool     DefaultEnabled { get; set; }
    public bool     IsEnabled      { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
