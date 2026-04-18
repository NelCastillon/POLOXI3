using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class AuditLog : AuditableEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string EventTypeCode { get; private set; } = string.Empty;
    public string ActionName { get; private set; } = string.Empty;
    public Guid? PerformedByUserId { get; private set; }

    private AuditLog() { }

    public AuditLog(Guid tenantId, string entityName, Guid entityId, string eventTypeCode, string actionName, Guid? performedByUserId)
        : base(tenantId, performedByUserId)
    {
        EntityName = entityName;
        EntityId = entityId;
        EventTypeCode = eventTypeCode;
        ActionName = actionName;
        PerformedByUserId = performedByUserId;
    }
}
