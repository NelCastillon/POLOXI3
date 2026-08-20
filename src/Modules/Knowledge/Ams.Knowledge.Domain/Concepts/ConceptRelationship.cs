using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Concepts;

public sealed class ConceptRelationship : KnowledgeRecord
{
    public ConceptRelationship(
        Guid id,
        Guid subjectConceptId,
        string predicateCode,
        Guid objectConceptId,
        decimal? relationshipStrength,
        string? source,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string statusCode,
        Guid? tenantId,
        bool isSystemDefined,
        Guid createdByUserId,
        DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        if (subjectConceptId == Guid.Empty || objectConceptId == Guid.Empty)
            throw new KnowledgeDomainException("Relationship subject and object concepts are required.");
        if (subjectConceptId == objectConceptId)
            throw new KnowledgeDomainException("A concept cannot relate to itself. Self hierarchy membership is represented only in the closure table.");
        if (relationshipStrength is < 0 or > 1)
            throw new KnowledgeDomainException("RelationshipStrength must be between 0 and 1.");

        KnowledgeGuard.EffectiveDates(effectiveFromUtc, effectiveToUtc);
        SubjectConceptId = subjectConceptId;
        PredicateCode = KnowledgeGuard.Code(predicateCode, "PredicateCode", 100);
        ObjectConceptId = objectConceptId;
        RelationshipStrength = relationshipStrength;
        Source = source?.Trim();
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
    }

    public Guid SubjectConceptId { get; }
    public string PredicateCode { get; }
    public Guid ObjectConceptId { get; }
    public decimal? RelationshipStrength { get; }
    public string? Source { get; }
    public DateTime EffectiveFromUtc { get; }
    public DateTime? EffectiveToUtc { get; }
    public string StatusCode { get; }
}
