using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Governance;

public sealed class KnowledgeChangeRequest : KnowledgeRecord
{
    public KnowledgeChangeRequest(Guid id, string entityTypeCode, Guid entityId, string changeTypeCode, string reason, string proposedChangeJson, string downstreamImpact, string statusCode, Guid? tenantId, bool isSystemDefined, Guid createdByUserId, DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        if (entityId == Guid.Empty)
            throw new KnowledgeDomainException("EntityId is required.");
        EntityTypeCode = KnowledgeGuard.Code(entityTypeCode, "EntityTypeCode", 100);
        EntityId = entityId;
        ChangeTypeCode = KnowledgeGuard.Code(changeTypeCode, "ChangeTypeCode", 50);
        Reason = KnowledgeGuard.Required(reason, "Reason", 1000);
        ProposedChangeJson = KnowledgeGuard.Required(proposedChangeJson, "ProposedChangeJson", int.MaxValue);
        DownstreamImpact = KnowledgeGuard.Required(downstreamImpact, "DownstreamImpact", 2000);
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
    }

    public string EntityTypeCode { get; }
    public Guid EntityId { get; }
    public string ChangeTypeCode { get; }
    public string Reason { get; }
    public string ProposedChangeJson { get; }
    public string DownstreamImpact { get; }
    public string StatusCode { get; private set; }

    public void TransitionTo(string statusCode, Guid actorUserId, DateTime modifiedUtc)
    {
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        MarkModified(actorUserId, modifiedUtc);
    }
}
