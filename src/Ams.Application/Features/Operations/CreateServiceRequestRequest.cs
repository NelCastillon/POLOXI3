namespace Ams.Application.Features.Operations;

public sealed class CreateServiceRequestRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid? EngagementId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string RequestTypeCode { get; set; } = "Servicing";
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PriorityCode { get; set; } = "Medium";
    public Guid? AssignedToUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
