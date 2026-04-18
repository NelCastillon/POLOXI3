namespace Ams.Application.Common.Dtos;

public sealed class AssistantMessageDto
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentDateUtc { get; set; }
}
