namespace Ams.Application.Common.Dtos;

public sealed class NotificationDto
{
    public Guid NotificationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipientUserId { get; set; }
    public Guid? TemplateId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadDateUtc { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
