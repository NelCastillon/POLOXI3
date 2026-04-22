namespace Ams.Application.Common.Dtos;

public sealed class WebhookEndpointDto
{
    public Guid WebhookEndpointId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string[] EventTypes { get; set; } = [];
    public bool IsActive { get; set; }
    public string? SecretHash { get; set; }
    public int DeliverySuccessCount { get; set; }
    public int DeliveryFailureCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? LastTriggeredUtc { get; set; }
}
