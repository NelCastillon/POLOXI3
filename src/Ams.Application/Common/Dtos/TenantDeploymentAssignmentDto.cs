namespace Ams.Application.Common.Dtos;

public sealed class TenantDeploymentAssignmentDto
{
    public Guid    AssignmentId      { get; set; }
    public Guid    TenantId          { get; set; }
    public string  TenantName        { get; set; } = string.Empty;
    public string  TenantCode        { get; set; } = string.Empty;
    public string  EnvironmentCode   { get; set; } = "Production";
    public string? PrimaryRegionCode { get; set; }
    public string? PrimaryRegionName { get; set; }
    public string? DrRegionCode      { get; set; }
    public string? DrRegionName      { get; set; }
    public string? StampCode         { get; set; }
    public string? StampName         { get; set; }
    public string? DatabaseCluster   { get; set; }
    public string? StorageBinding    { get; set; }
    public string  IsolationMode     { get; set; } = "Shared";
    public string  StatusCode        { get; set; } = "Active";
    public string? Notes             { get; set; }
    public DateTime  CreatedDateUtc  { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
