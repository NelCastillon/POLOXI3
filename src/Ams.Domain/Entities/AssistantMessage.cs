namespace Ams.Domain.Entities;

public sealed class AssistantMessage
{
    public Guid MessageId { get; private set; } = Guid.NewGuid();
    public Guid ConversationId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Role { get; private set; } = "user";
    public string Content { get; private set; } = string.Empty;
    public DateTime SentDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private AssistantMessage() { }

    public AssistantMessage(Guid conversationId, Guid tenantId, string role, string content)
    {
        ConversationId = conversationId;
        TenantId = tenantId;
        Role = role;
        Content = content;
        SentDateUtc = DateTime.UtcNow;
    }
}
