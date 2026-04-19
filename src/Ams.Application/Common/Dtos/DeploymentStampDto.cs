namespace Ams.Application.Common.Dtos;

public sealed class DeploymentStampDto
{
    public Guid   StampId           { get; set; }
    public string StampCode         { get; set; } = string.Empty;
    public string StampName         { get; set; } = string.Empty;
    public Guid?  RegionId          { get; set; }
    public string RegionCode        { get; set; } = string.Empty;
    public string RegionName        { get; set; } = string.Empty;
    public string EnvironmentCode   { get; set; } = string.Empty;
    public string StatusCode        { get; set; } = string.Empty;
    public int    TenantCount       { get; set; }
    public int    MaxTenantCapacity { get; set; }
    public decimal LoadPercent      { get; set; }
    public int    ActiveServices    { get; set; }
    public string? Notes            { get; set; }
    public bool   IsActive          { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
