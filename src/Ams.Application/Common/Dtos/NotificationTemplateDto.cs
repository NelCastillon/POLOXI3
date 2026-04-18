namespace Ams.Application.Common.Dtos;

public sealed class NotificationTemplateDto
{
    public Guid TemplateId { get; set; }
    public Guid? TenantId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string ChannelCode { get; set; } = string.Empty;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsSystemTemplate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
