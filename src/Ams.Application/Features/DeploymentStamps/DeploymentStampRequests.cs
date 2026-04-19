namespace Ams.Application.Features.DeploymentStamps;

public sealed class CreateDeploymentStampRequest
{
    public string  StampCode         { get; set; } = string.Empty;
    public string  StampName         { get; set; } = string.Empty;
    public string  RegionCode        { get; set; } = string.Empty;
    public string  EnvironmentCode   { get; set; } = "Production";
    public string  StatusCode        { get; set; } = "Active";
    public int     MaxTenantCapacity { get; set; }
    public string? Notes             { get; set; }
    public Guid?   CreatedByUserId   { get; set; }
}

public sealed class UpdateDeploymentStampRequest
{
    public string  StampCode         { get; set; } = string.Empty;
    public string  StampName         { get; set; } = string.Empty;
    public string  RegionCode        { get; set; } = string.Empty;
    public string  EnvironmentCode   { get; set; } = "Production";
    public int     MaxTenantCapacity { get; set; }
    public decimal LoadPercent       { get; set; }
    public int     ActiveServices    { get; set; }
    public string? Notes             { get; set; }
    public Guid?   ModifiedByUserId  { get; set; }
}
