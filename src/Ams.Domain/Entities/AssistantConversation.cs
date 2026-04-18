using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class AssistantConversation : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string? ContextEntityName { get; private set; }
    public Guid? ContextEntityId { get; private set; }

    private AssistantConversation() { }

    public AssistantConversation(Guid tenantId, Guid userId, string? contextEntityName, Guid? contextEntityId, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        UserId = userId;
        ContextEntityName = contextEntityName;
        ContextEntityId = contextEntityId;
    }
}
