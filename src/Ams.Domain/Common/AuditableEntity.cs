namespace Ams.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid TenantId { get; protected set; }
    public DateTime CreatedDateUtc { get; protected set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; protected set; }
    public DateTime? ModifiedDateUtc { get; protected set; }
    public Guid? ModifiedByUserId { get; protected set; }
    public bool IsDeleted { get; protected set; }

    protected AuditableEntity() { }

    protected AuditableEntity(Guid tenantId, Guid? createdByUserId)
    {
        TenantId = tenantId;
        CreatedByUserId = createdByUserId;
        CreatedDateUtc = DateTime.UtcNow;
    }

    public void MarkModified(Guid? modifiedByUserId)
    {
        ModifiedByUserId = modifiedByUserId;
        ModifiedDateUtc = DateTime.UtcNow;
    }

    public void SoftDelete(Guid? modifiedByUserId)
    {
        IsDeleted = true;
        MarkModified(modifiedByUserId);
    }
}
