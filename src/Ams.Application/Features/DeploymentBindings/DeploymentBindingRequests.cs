namespace Ams.Application.Features.DeploymentBindings;

public sealed class CreateDeploymentBindingRequest
{
    public Guid    TenantId        { get; set; }
    public string  RegionCode      { get; set; } = string.Empty;
    public string  EnvironmentCode { get; set; } = "Production";
    public string? StampCode       { get; set; }
    public string  IsolationMode   { get; set; } = "Shared";
    public bool    IsPrimary       { get; set; }
    public string  StatusCode      { get; set; } = "Active";
    public string? Notes           { get; set; }
}

public sealed class UpdateDeploymentBindingRequest
{
    public string  RegionCode      { get; set; } = string.Empty;
    public string  EnvironmentCode { get; set; } = "Production";
    public string? StampCode       { get; set; }
    public string  IsolationMode   { get; set; } = "Shared";
    public bool    IsPrimary       { get; set; }
    public string  StatusCode      { get; set; } = "Active";
    public string? Notes           { get; set; }
}
