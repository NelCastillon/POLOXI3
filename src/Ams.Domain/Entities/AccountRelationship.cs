using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class AccountRelationship : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid RelatedAccountId { get; private set; }
    public string RelationshipType { get; private set; } = string.Empty; // Parent, Subsidiary, Partner, Affiliated, Referred By
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }

    private AccountRelationship() { }

    public AccountRelationship(
        Guid tenantId,
        Guid accountId,
        Guid relatedAccountId,
        string relationshipType,
        Guid? createdByUserId,
        string? description = null,
        DateTime? startedAtUtc = null)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        RelatedAccountId = relatedAccountId;
        RelationshipType = relationshipType;
        Description = description;
        StartedAtUtc = startedAtUtc ?? DateTime.UtcNow;
    }

    public void Deactivate(DateTime endedAtUtc, Guid? modifiedByUserId)
    {
        IsActive = false;
        EndedAtUtc = endedAtUtc;
        MarkModified(modifiedByUserId);
    }

    public void Update(string relationshipType, string? description, Guid? modifiedByUserId)
    {
        RelationshipType = relationshipType;
        Description = description;
        MarkModified(modifiedByUserId);
    }
}
