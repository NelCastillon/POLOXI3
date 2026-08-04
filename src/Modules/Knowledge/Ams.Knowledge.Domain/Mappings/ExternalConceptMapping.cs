using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Mappings;

public sealed class ExternalConceptMapping : KnowledgeRecord
{
    public ExternalConceptMapping(
        Guid id,
        Guid knowledgeConceptId,
        string sourceSystemTypeCode,
        Guid? sourceSystemId,
        string? externalCode,
        string externalValue,
        string? externalPath,
        string mappingDirectionCode,
        string matchTypeCode,
        decimal? confidenceScore,
        string? stateCode,
        Guid? lineOfBusinessConceptId,
        Guid? carrierProductId,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        Guid? tenantId,
        bool isSystemDefined,
        Guid createdByUserId,
        DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        if (knowledgeConceptId == Guid.Empty)
            throw new KnowledgeDomainException("KnowledgeConceptId is required.");
        if (confidenceScore is < 0 or > 1)
            throw new KnowledgeDomainException("ConfidenceScore must be between 0 and 1.");
        if (string.IsNullOrWhiteSpace(externalCode) && string.IsNullOrWhiteSpace(externalValue))
            throw new KnowledgeDomainException("An external code or value is required.");
        KnowledgeGuard.EffectiveDates(effectiveFromUtc, effectiveToUtc);

        KnowledgeConceptId = knowledgeConceptId;
        SourceSystemTypeCode = KnowledgeGuard.Code(sourceSystemTypeCode, "SourceSystemTypeCode", 50);
        SourceSystemId = sourceSystemId;
        ExternalCode = externalCode?.Trim();
        ExternalValue = KnowledgeGuard.Required(externalValue, "ExternalValue", 500);
        NormalizedExternalValue = KnowledgeGuard.NormalizedLabel(externalValue);
        ExternalPath = externalPath?.Trim();
        MappingDirectionCode = KnowledgeGuard.Code(mappingDirectionCode, "MappingDirectionCode", 20);
        MatchTypeCode = KnowledgeGuard.Code(matchTypeCode, "MatchTypeCode", 30);
        ConfidenceScore = confidenceScore;
        StateCode = stateCode?.Trim().ToUpperInvariant();
        LineOfBusinessConceptId = lineOfBusinessConceptId;
        CarrierProductId = carrierProductId;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
    }

    public Guid KnowledgeConceptId { get; }
    public string SourceSystemTypeCode { get; }
    public Guid? SourceSystemId { get; }
    public string? ExternalCode { get; }
    public string ExternalValue { get; }
    public string NormalizedExternalValue { get; }
    public string? ExternalPath { get; }
    public string MappingDirectionCode { get; }
    public string MatchTypeCode { get; }
    public decimal? ConfidenceScore { get; }
    public string? StateCode { get; }
    public Guid? LineOfBusinessConceptId { get; }
    public Guid? CarrierProductId { get; }
    public DateTime EffectiveFromUtc { get; }
    public DateTime? EffectiveToUtc { get; }
    public bool IsApproved { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedUtc { get; private set; }

    public void Approve(Guid actorUserId, DateTime approvedUtc)
    {
        if (actorUserId == Guid.Empty)
            throw new KnowledgeDomainException("An approving user is required.");
        IsApproved = true;
        ApprovedByUserId = actorUserId;
        ApprovedUtc = approvedUtc;
        MarkModified(actorUserId, approvedUtc);
    }

    public void RevokeApproval(Guid actorUserId, DateTime modifiedUtc)
    {
        IsApproved = false;
        ApprovedByUserId = null;
        ApprovedUtc = null;
        MarkModified(actorUserId, modifiedUtc);
    }
}
