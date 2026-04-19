namespace Ams.Application.Common.Dtos;

public sealed class PlanFeatureDto
{
    public Guid    PlanFeatureId  { get; set; }
    public Guid    PlanId         { get; set; }
    public string  FeatureCode    { get; set; } = string.Empty;
    public string  FeatureName    { get; set; } = string.Empty;
    public bool    IsIncluded     { get; set; }
    public string  Notes          { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
