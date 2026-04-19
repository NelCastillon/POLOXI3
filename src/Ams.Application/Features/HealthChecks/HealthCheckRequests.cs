namespace Ams.Application.Features.HealthChecks;

public sealed class CreateHealthCheckRequest
{
    public string  ServiceName     { get; set; } = string.Empty;
    public string? EndpointUrl     { get; set; }
    public string  StatusCode      { get; set; } = "Unknown";
    public int     LatencyMs       { get; set; }
    public decimal UptimePercent   { get; set; } = 100.00m;
    public string? RegionCode      { get; set; }
    public string  EnvironmentCode { get; set; } = "Production";
    public string? Notes           { get; set; }
    public Guid?   CreatedByUserId { get; set; }
}

public sealed class UpdateHealthCheckRequest
{
    public string  ServiceName     { get; set; } = string.Empty;
    public string? EndpointUrl     { get; set; }
    public string  StatusCode      { get; set; } = "Unknown";
    public int     LatencyMs       { get; set; }
    public decimal UptimePercent   { get; set; }
    public string? RegionCode      { get; set; }
    public string  EnvironmentCode { get; set; } = "Production";
    public bool    IsActive        { get; set; }
    public string? Notes           { get; set; }
}
