namespace Ams.Application.Common.Dtos;

public sealed class ServiceRequestDto
{
    public Guid ServiceRequestId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public Guid? EngagementId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string RequestTypeCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateOnly? ResolvedDate { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
