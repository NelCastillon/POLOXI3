namespace Ams.Domain.Entities;

public sealed class DocumentAccessLog
{
    public Guid AccessLogId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? ShareLinkId { get; private set; }
    public string ActionCode { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public DateTime AccessDateUtc { get; private set; } = DateTime.UtcNow;

    private DocumentAccessLog() { }

    public DocumentAccessLog(Guid tenantId, Guid documentId, Guid? userId, Guid? shareLinkId, string actionCode, string? ipAddress)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        UserId = userId;
        ShareLinkId = shareLinkId;
        ActionCode = actionCode;
        IpAddress = ipAddress;
    }
}
