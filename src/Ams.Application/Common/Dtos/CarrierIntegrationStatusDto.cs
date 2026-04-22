namespace Ams.Application.Common.Dtos;

public sealed class CarrierIntegrationStatusDto
{
    public Guid CarrierIntegrationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string ConnectionStatus { get; set; } = string.Empty;
    public string? LastCheckedUtc { get; set; }
    public string? LastSuccessUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public double UptimePercent { get; set; }
}
