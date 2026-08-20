namespace Ams.Knowledge.Domain.Common;

public abstract class KnowledgeRecord
{
    protected KnowledgeRecord(Guid id, Guid? tenantId, bool isSystemDefined, Guid createdByUserId, DateTime createdUtc)
    {
        if (id == Guid.Empty)
            throw new KnowledgeDomainException("A knowledge record identifier is required.");
        if (createdByUserId == Guid.Empty)
            throw new KnowledgeDomainException("CreatedByUserId is required.");

        KnowledgeGuard.TenantScope(tenantId, isSystemDefined);
        Id = id;
        TenantId = tenantId;
        IsSystemDefined = isSystemDefined;
        CreatedByUserId = createdByUserId;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; }
    public Guid? TenantId { get; }
    public bool IsSystemDefined { get; }
    public Guid CreatedByUserId { get; }
    public DateTime CreatedUtc { get; }
    public Guid? ModifiedByUserId { get; protected set; }
    public DateTime? ModifiedUtc { get; protected set; }
    public bool IsDeleted { get; protected set; }

    protected void MarkModified(Guid actorUserId, DateTime modifiedUtc)
    {
        if (actorUserId == Guid.Empty)
            throw new KnowledgeDomainException("An actor user identifier is required.");
        ModifiedByUserId = actorUserId;
        ModifiedUtc = modifiedUtc;
    }
}
