using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Concepts;

public sealed record ConceptVersion(
    Guid ConceptVersionId,
    Guid KnowledgeConceptId,
    int VersionNumber,
    string ConceptCode,
    string PreferredLabel,
    string? Definition,
    string StatusCode,
    string SnapshotJson,
    string ChangeReason,
    Guid CreatedByUserId,
    DateTime CreatedUtc)
{
    public ConceptVersion Validate()
    {
        if (ConceptVersionId == Guid.Empty || KnowledgeConceptId == Guid.Empty)
            throw new KnowledgeDomainException("Concept version identifiers are required.");
        if (VersionNumber < 1)
            throw new KnowledgeDomainException("VersionNumber must be at least one.");
        KnowledgeGuard.Code(ConceptCode, "ConceptCode", 100);
        KnowledgeGuard.Required(PreferredLabel, "PreferredLabel", 250);
        KnowledgeGuard.Required(StatusCode, "StatusCode", 30);
        KnowledgeGuard.Required(SnapshotJson, "SnapshotJson", int.MaxValue);
        KnowledgeGuard.Required(ChangeReason, "ChangeReason", 1000);
        return this;
    }
}
