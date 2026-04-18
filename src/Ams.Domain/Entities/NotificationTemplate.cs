namespace Ams.Domain.Entities;

public sealed class NotificationTemplate
{
    public Guid TemplateId { get; private set; } = Guid.NewGuid();
    public Guid? TenantId { get; private set; }
    public string TemplateCode { get; private set; } = string.Empty;
    public string TemplateName { get; private set; } = string.Empty;
    public string ChannelCode { get; private set; } = "Email";
    public string? SubjectTemplate { get; private set; }
    public string BodyTemplate { get; private set; } = string.Empty;
    public bool IsSystemTemplate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }

    private NotificationTemplate() { }

    public NotificationTemplate(string templateCode, string templateName, string channelCode, string bodyTemplate)
    {
        TemplateCode = templateCode;
        TemplateName = templateName;
        ChannelCode = channelCode;
        BodyTemplate = bodyTemplate;
    }
}
