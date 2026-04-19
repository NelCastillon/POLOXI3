namespace Ams.Application.Common.Dtos;

public sealed class RegionDto
{
    public Guid      RegionId          { get; set; }
    public string    RegionCode        { get; set; } = string.Empty;
    public string    RegionName        { get; set; } = string.Empty;
    public string?   CloudRegion       { get; set; }
    public string?   ComplianceProfile { get; set; }
    public string?   PrimaryStamp      { get; set; }
    public string?   SecondaryStamp    { get; set; }
    public bool      IsActive          { get; set; }
    public DateTime  CreatedDateUtc    { get; set; }
    public DateTime? ModifiedDateUtc   { get; set; }
}
