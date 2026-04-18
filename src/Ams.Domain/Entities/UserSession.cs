namespace Ams.Domain.Entities;

public sealed class UserSession
{
    public Guid SessionId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string SessionToken { get; private set; } = string.Empty;
    public string? DeviceIdentifier { get; private set; }
    public string? DeviceType { get; private set; }
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime LoginDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LastActivityDateUtc { get; private set; }
    public DateTime ExpiresDateUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedDateUtc { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private UserSession() { }

    public UserSession(Guid tenantId, Guid userId, string sessionToken, DateTime expiresDateUtc)
    {
        TenantId = tenantId;
        UserId = userId;
        SessionToken = sessionToken;
        ExpiresDateUtc = expiresDateUtc;
    }
}
