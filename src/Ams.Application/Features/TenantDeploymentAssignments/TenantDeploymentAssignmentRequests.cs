namespace Ams.Application.Features.TenantDeploymentAssignments;

public sealed class UpsertTenantDeploymentAssignmentRequest
{
    public Guid    TenantId          { get; set; }
    public string  EnvironmentCode   { get; set; } = "Production";
    public string? PrimaryRegionCode { get; set; }
    public string? DrRegionCode      { get; set; }
    public string? StampCode         { get; set; }
    public string? DatabaseCluster   { get; set; }
    public string? StorageBinding    { get; set; }
    public string  IsolationMode     { get; set; } = "Shared";
    public string  StatusCode        { get; set; } = "Active";
    public string? Notes             { get; set; }
    public Guid?   CreatedByUserId   { get; set; }
}
