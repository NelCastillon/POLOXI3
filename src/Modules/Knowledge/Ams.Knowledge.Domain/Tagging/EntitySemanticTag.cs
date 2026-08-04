using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Tagging;

public sealed class EntitySemanticTag : KnowledgeRecord
{
    public EntitySemanticTag(Guid id, Guid tenantId, string entityTypeCode, Guid entityId, Guid knowledgeConceptId, int conceptVersionNumber, string tagSourceCode, decimal? confidenceScore, Guid createdByUserId, DateTime createdUtc)
        : base(id, tenantId, false, createdByUserId, createdUtc)
    {
        if (entityId == Guid.Empty || knowledgeConceptId == Guid.Empty)
            throw new KnowledgeDomainException("Entity and concept identifiers are required.");
        if (conceptVersionNumber < 1)
            throw new KnowledgeDomainException("ConceptVersionNumber must be at least one.");
        if (confidenceScore is < 0 or > 1)
            throw new KnowledgeDomainException("ConfidenceScore must be between 0 and 1.");

        EntityTypeCode = KnowledgeGuard.Code(entityTypeCode, "EntityTypeCode", 100);
        EntityId = entityId;
        KnowledgeConceptId = knowledgeConceptId;
        ConceptVersionNumber = conceptVersionNumber;
        TagSourceCode = KnowledgeGuard.Code(tagSourceCode, "TagSourceCode", 30);
        ConfidenceScore = confidenceScore;
    }

    public string EntityTypeCode { get; }
    public Guid EntityId { get; }
    public Guid KnowledgeConceptId { get; }
    public int ConceptVersionNumber { get; }
    public string TagSourceCode { get; }
    public decimal? ConfidenceScore { get; }
    public bool IsVerified { get; private set; }
    public Guid? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedUtc { get; private set; }

    public void Verify(Guid actorUserId, DateTime verifiedUtc)
    {
        IsVerified = true;
        VerifiedByUserId = actorUserId;
        VerifiedUtc = verifiedUtc;
        MarkModified(actorUserId, verifiedUtc);
    }
}
