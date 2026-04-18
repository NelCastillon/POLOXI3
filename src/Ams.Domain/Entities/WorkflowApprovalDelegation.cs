using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowApprovalDelegation : AuditableEntity
{
    public Guid DelegatorUserId { get; private set; }
    public Guid DelegateUserId { get; private set; }
    public Guid? WorkflowDefinitionId { get; private set; }
    public DateTime DelegationStartDateUtc { get; private set; }
    public DateTime DelegationEndDateUtc { get; private set; }
    public string? Reason { get; private set; }
    public bool IsActive { get; private set; }

    private WorkflowApprovalDelegation() { }

    public WorkflowApprovalDelegation(Guid tenantId, Guid delegatorUserId, Guid delegateUserId, DateTime startDateUtc, DateTime endDateUtc, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        DelegatorUserId = delegatorUserId;
        DelegateUserId = delegateUserId;
        DelegationStartDateUtc = startDateUtc;
        DelegationEndDateUtc = endDateUtc;
        IsActive = true;
    }
}
