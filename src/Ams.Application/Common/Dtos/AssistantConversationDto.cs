namespace Ams.Application.Common.Dtos;

public sealed class AssistantConversationDto
{
    public Guid AssistantConversationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? ContextEntityName { get; set; }
    public Guid? ContextEntityId { get; set; }
    public DateTime StartedDateUtc { get; set; }
}
