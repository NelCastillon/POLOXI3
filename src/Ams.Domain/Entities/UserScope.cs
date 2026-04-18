using Ams.Domain.Common;

namespace Ams.Domain.Entities;

/// <summary>
/// Restricts a user's visibility to a specific organisational scope.
/// ScopeTypeCode: Tenant | Company | Branch | Department | BusinessUnit |
///                OwnedRecords | AssignedRecords | Region | LineOfBusiness
/// </summary>
public sealed class UserScope : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string ScopeTypeCode { get; private set; } = string.Empty;
    public string ScopeValue { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public Guid? GrantedByUserId { get; private set; }
    public DateTime GrantedDateUtc { get; private set; }
    public DateTime? ExpiresDateUtc { get; private set; }

    private UserScope() { }

    public UserScope(Guid tenantId, Guid userId, string scopeTypeCode, string scopeValue,
        Guid? grantedByUserId, DateTime? expiresDateUtc = null)
        : base(tenantId, grantedByUserId)
    {
        UserId = userId;
        ScopeTypeCode = scopeTypeCode;
        ScopeValue = scopeValue;
        GrantedByUserId = grantedByUserId;
        GrantedDateUtc = DateTime.UtcNow;
        ExpiresDateUtc = expiresDateUtc;
    }

    public void Revoke(Guid? modifiedByUserId)
    {
        IsActive = false;
        ExpiresDateUtc ??= DateTime.UtcNow;
        MarkModified(modifiedByUserId);
    }
}
