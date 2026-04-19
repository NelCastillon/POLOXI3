namespace Ams.Application.Features.Regions;

public sealed class CreateRegionRequest
{
    public string  RegionCode        { get; set; } = string.Empty;
    public string  RegionName        { get; set; } = string.Empty;
    public string? CloudRegion       { get; set; }
    public string? ComplianceProfile { get; set; }
    public string? PrimaryStamp      { get; set; }
    public string? SecondaryStamp    { get; set; }
    public bool    IsActive          { get; set; } = true;
}

public sealed class UpdateRegionRequest
{
    public string  RegionName        { get; set; } = string.Empty;
    public string? CloudRegion       { get; set; }
    public string? ComplianceProfile { get; set; }
    public string? PrimaryStamp      { get; set; }
    public string? SecondaryStamp    { get; set; }
}
