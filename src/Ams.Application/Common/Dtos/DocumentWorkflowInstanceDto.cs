namespace Ams.Application.Common.Dtos;

public sealed record DocumentWorkflowInstanceDto
{
    public Guid WorkflowInstanceId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid WorkflowTemplateId { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string WorkflowStatus { get; init; } = string.Empty;
    public int? CurrentStepOrder { get; init; }
    public DateTime StartedDateUtc { get; init; }
    public DateTime? CompletedDateUtc { get; init; }
    public DateTime? DueDateUtc { get; init; }
    public Guid InitiatedByUserId { get; init; }
    public string? InitiatedByName { get; init; }
    public string? Comments { get; init; }
    public string Priority { get; init; } = string.Empty;
    public string? FinalOutcome { get; init; }
    public string? FinalComments { get; init; }
    public Guid? CompletedByUserId { get; init; }
    public string? CompletedByName { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public DateTime? ModifiedDateUtc { get; init; }
}
