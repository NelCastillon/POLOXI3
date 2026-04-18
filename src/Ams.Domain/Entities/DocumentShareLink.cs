namespace Ams.Domain.Entities;

public sealed class DocumentShareLink
{
    public Guid ShareLinkId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTime ExpiresDateUtc { get; private set; }
    public int? MaxAccessCount { get; private set; }
    public int AccessCount { get; private set; }
    public bool RequiresPin { get; private set; }
    public string? PinHash { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedDateUtc { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private DocumentShareLink() { }

    public DocumentShareLink(Guid tenantId, Guid documentId, string token, Guid createdByUserId, DateTime expiresDateUtc, int? maxAccessCount, bool requiresPin, string? pinHash)
    {
        TenantId = tenantId;
        DocumentId = documentId;
        Token = token;
        CreatedByUserId = createdByUserId;
        ExpiresDateUtc = expiresDateUtc;
        MaxAccessCount = maxAccessCount;
        RequiresPin = requiresPin;
        PinHash = pinHash;
    }

    public void Revoke()
    {
        IsRevoked = true;
        RevokedDateUtc = DateTime.UtcNow;
    }

    public bool IsValid() =>
        !IsRevoked &&
        !IsDeleted &&
        DateTime.UtcNow < ExpiresDateUtc &&
        (MaxAccessCount is null || AccessCount < MaxAccessCount);
}
