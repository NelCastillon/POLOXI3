using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Communications;

public sealed class CreateNotificationRequest
{
    public Guid TenantId { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid? TemplateId { get; set; }

    [Required, StringLength(50)]
    public string ChannelCode { get; set; } = "InApp";

    [StringLength(200)]
    public string? Subject { get; set; }

    [Required, StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(100)]
    public string? EntityName { get; set; }

    public Guid? EntityId { get; set; }

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Delivered";

    public DateTime? SentDateUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CreatedByUserId { get; set; }
    [StringLength(40)]
    public string Priority { get; set; } = "Normal";
    [StringLength(80)]
    public string Category { get; set; } = "General";
}

public sealed record NotificationStatusRequest(string StatusCode, string? Reason = null);

public sealed record NotificationRetryRequest(string? ProviderName = null);
