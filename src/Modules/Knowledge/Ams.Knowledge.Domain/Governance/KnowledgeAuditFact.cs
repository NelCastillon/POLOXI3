using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Governance;

public sealed record KnowledgeAuditFact(
    Guid TenantId,
    Guid ActorUserId,
    string ActionTypeCode,
    string EntityTypeCode,
    Guid EntityId,
    string? OldValueJson,
    string? NewValueJson,
    string ChangeReason,
    string SourceCode,
    string CorrelationId,
    int? VersionNumber,
    DateTime OccurredUtc)
{
    public KnowledgeAuditFact Validate()
    {
        if (TenantId == Guid.Empty || ActorUserId == Guid.Empty || EntityId == Guid.Empty)
            throw new KnowledgeDomainException("Audit tenant, actor, and entity identifiers are required.");
        KnowledgeGuard.Code(ActionTypeCode, "ActionTypeCode", 100);
        KnowledgeGuard.Code(EntityTypeCode, "EntityTypeCode", 100);
        KnowledgeGuard.Required(ChangeReason, "ChangeReason", 1000);
        KnowledgeGuard.Code(SourceCode, "SourceCode", 100);
        KnowledgeGuard.Required(CorrelationId, "CorrelationId", 120);
        return this;
    }
}
