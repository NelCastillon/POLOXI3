namespace Ams.Application.Common.Dtos;

public sealed class WorkflowInstanceDto
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid TenantId { get; set; }
    public string TargetEntityName { get; set; } = string.Empty;
    public Guid TargetEntityId { get; set; }
    public int StatusCode { get; set; }
    public DateTime SubmittedDateUtc { get; set; }
}
