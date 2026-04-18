using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class WorkflowInstance : AuditableEntity
{
    public string TargetEntityName { get; private set; } = string.Empty;
    public Guid TargetEntityId { get; private set; }
    public WorkflowStatus Status { get; private set; }

    private WorkflowInstance() { }

    public WorkflowInstance(Guid tenantId, string targetEntityName, Guid targetEntityId, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        TargetEntityName = targetEntityName;
        TargetEntityId = targetEntityId;
        Status = WorkflowStatus.Submitted;
    }
}
