namespace Ams.Application.Common.Dtos;

public sealed class HealthCheckDto
{
    public Guid    HealthCheckId    { get; set; }
    public string  ServiceName      { get; set; } = string.Empty;
    public string? EndpointUrl      { get; set; }
    public string  StatusCode       { get; set; } = string.Empty;
    public int     LatencyMs        { get; set; }
    public decimal UptimePercent    { get; set; }
    public DateTime LastCheckDateUtc { get; set; }
    public string? Notes            { get; set; }
    public string? RegionCode       { get; set; }
    public string  EnvironmentCode  { get; set; } = string.Empty;
    public bool    IsActive         { get; set; }
    public DateTime  CreatedDateUtc  { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
