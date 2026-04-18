namespace Ams.Domain.Entities;

public sealed class Notification
{
    public Guid NotificationId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string ChannelCode { get; private set; } = "InApp";
    public string? Subject { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string? EntityName { get; private set; }
    public Guid? EntityId { get; private set; }
    public string StatusCode { get; private set; } = "Pending";
    public bool IsRead { get; private set; }
    public DateTime? ReadDateUtc { get; private set; }
    public DateTime? SentDateUtc { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }

    private Notification() { }

    public Notification(Guid tenantId, Guid recipientUserId, string channelCode, string body)
    {
        TenantId = tenantId;
        RecipientUserId = recipientUserId;
        ChannelCode = channelCode;
        Body = body;
    }
}
