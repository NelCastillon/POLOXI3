namespace Ams.Application.Common.Dtos;

public sealed class WorkflowApprovalDelegationDto
{
    public Guid DelegationId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DelegatorUserId { get; init; }
    public Guid DelegateUserId { get; init; }
    public Guid? WorkflowDefinitionId { get; init; }
    public DateTime DelegationStartDateUtc { get; init; }
    public DateTime DelegationEndDateUtc { get; init; }
    public string? Reason { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
