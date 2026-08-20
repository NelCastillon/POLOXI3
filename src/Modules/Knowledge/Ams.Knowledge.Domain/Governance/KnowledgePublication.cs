using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Governance;

public sealed class KnowledgePublication : KnowledgeRecord
{
    private readonly List<KnowledgePublicationItem> _items = [];

    public KnowledgePublication(Guid id, string publicationCode, string name, string versionLabel, string statusCode, Guid? tenantId, bool isSystemDefined, Guid createdByUserId, DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        PublicationCode = KnowledgeGuard.Code(publicationCode, "PublicationCode", 100);
        Name = KnowledgeGuard.Required(name, "Name", 200);
        VersionLabel = KnowledgeGuard.Required(versionLabel, "VersionLabel", 50);
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
    }

    public string PublicationCode { get; }
    public string Name { get; }
    public string VersionLabel { get; }
    public string StatusCode { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedUtc { get; private set; }
    public IReadOnlyCollection<KnowledgePublicationItem> Items => _items.AsReadOnly();

    public void AddItem(KnowledgePublicationItem item)
    {
        if (PublishedUtc.HasValue)
            throw new KnowledgeDomainException("Published snapshots are immutable.");
        if (item.PublicationId != Id)
            throw new KnowledgeDomainException("The publication item belongs to a different publication.");
        if (_items.Any(existing => existing.EntityTypeCode == item.EntityTypeCode && existing.EntityId == item.EntityId && existing.VersionNumber == item.VersionNumber))
            throw new KnowledgeDomainException("The publication already contains this entity version.");
        _items.Add(item);
    }

    public void Publish(string publishedStatusCode, Guid actorUserId, DateTime publishedUtc)
    {
        if (_items.Count == 0)
            throw new KnowledgeDomainException("A publication must contain at least one versioned item.");
        if (PublishedUtc.HasValue)
            throw new KnowledgeDomainException("The publication has already been published.");
        StatusCode = KnowledgeGuard.Code(publishedStatusCode, "StatusCode", 30);
        PublishedByUserId = actorUserId;
        PublishedUtc = publishedUtc;
        MarkModified(actorUserId, publishedUtc);
    }
}

public sealed record KnowledgePublicationItem(Guid PublicationItemId, Guid PublicationId, string EntityTypeCode, Guid EntityId, int VersionNumber, string SnapshotJson)
{
    public KnowledgePublicationItem Validate()
    {
        if (PublicationItemId == Guid.Empty || PublicationId == Guid.Empty || EntityId == Guid.Empty)
            throw new KnowledgeDomainException("Publication item identifiers are required.");
        if (VersionNumber < 1)
            throw new KnowledgeDomainException("VersionNumber must be at least one.");
        KnowledgeGuard.Code(EntityTypeCode, "EntityTypeCode", 100);
        KnowledgeGuard.Required(SnapshotJson, "SnapshotJson", int.MaxValue);
        return this;
    }
}
