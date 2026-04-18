using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class ServiceRequest : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public Guid? EngagementId { get; private set; }
    public string RequestNumber { get; private set; } = string.Empty;
    public ServiceRequestType RequestType { get; private set; } = ServiceRequestType.Servicing;
    public string Subject { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string PriorityCode { get; private set; } = "Medium";
    public Guid? AssignedToUserId { get; private set; }
    public ServiceRequestStatus Status { get; private set; } = ServiceRequestStatus.Open;
    public DateOnly? ResolvedDate { get; private set; }

    private ServiceRequest() { }

    public ServiceRequest(Guid tenantId, Guid accountId, string requestNumber, ServiceRequestType requestType, string subject, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        RequestNumber = requestNumber;
        RequestType = requestType;
        Subject = subject;
        PriorityCode = "Medium";
        Status = ServiceRequestStatus.Open;
    }
}
