using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Concepts;

public sealed class ConceptLabel : KnowledgeRecord
{
    public ConceptLabel(
        Guid id,
        Guid knowledgeConceptId,
        string label,
        string labelTypeCode,
        string languageCode,
        string? source,
        bool isSearchable,
        bool isDeprecated,
        Guid? tenantId,
        bool isSystemDefined,
        Guid createdByUserId,
        DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        if (knowledgeConceptId == Guid.Empty)
            throw new KnowledgeDomainException("KnowledgeConceptId is required.");

        KnowledgeConceptId = knowledgeConceptId;
        Label = KnowledgeGuard.Required(label, "Label", 250);
        NormalizedLabel = KnowledgeGuard.NormalizedLabel(label);
        LabelTypeCode = KnowledgeGuard.Code(labelTypeCode, "LabelTypeCode", 30);
        LanguageCode = KnowledgeGuard.Required(languageCode, "LanguageCode", 10).ToLowerInvariant();
        Source = source?.Trim();
        IsSearchable = isSearchable;
        IsDeprecated = isDeprecated;
    }

    public Guid KnowledgeConceptId { get; }
    public string Label { get; }
    public string NormalizedLabel { get; }
    public string LabelTypeCode { get; }
    public string LanguageCode { get; }
    public string? Source { get; }
    public bool IsSearchable { get; }
    public bool IsDeprecated { get; }
    public bool IsPreferred => LabelTypeCode.Equals("PREFERRED", StringComparison.OrdinalIgnoreCase);
}
