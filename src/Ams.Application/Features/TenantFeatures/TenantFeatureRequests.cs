namespace Ams.Application.Features.TenantFeatures;

public sealed class OverrideTenantFeatureRequest
{
    public string    FeatureCode       { get; set; } = string.Empty;
    public bool      IsEnabled         { get; set; }
    public DateTime? EffectiveStartUtc { get; set; }
    public DateTime? EffectiveEndUtc   { get; set; }
    public string    SourceType        { get; set; } = "Override";
}
