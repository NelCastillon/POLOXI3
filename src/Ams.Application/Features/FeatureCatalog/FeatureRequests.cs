namespace Ams.Application.Features.FeatureCatalog;

public sealed class CreateFeatureRequest
{
    public string FeatureCode    { get; set; } = string.Empty;
    public string FeatureName    { get; set; } = string.Empty;
    public string Module         { get; set; } = string.Empty;
    public string TypeCode       { get; set; } = "Toggle";
    public bool   DefaultEnabled { get; set; }
    public bool   IsEnabled      { get; set; } = true;
}

public sealed class UpdateFeatureRequest
{
    public string FeatureName    { get; set; } = string.Empty;
    public string Module         { get; set; } = string.Empty;
    public string TypeCode       { get; set; } = "Toggle";
    public bool   DefaultEnabled { get; set; }
}
