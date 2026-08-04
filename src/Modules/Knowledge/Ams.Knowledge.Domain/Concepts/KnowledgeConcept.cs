using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Concepts;

public sealed class KnowledgeConcept : KnowledgeRecord
{
    private readonly List<ConceptLabel> _labels = [];

    public KnowledgeConcept(
        Guid id,
        Guid conceptSchemeId,
        string conceptCode,
        string conceptTypeCode,
        string preferredLabel,
        string? definition,
        Guid? parentConceptId,
        bool isAbstract,
        bool isSelectable,
        string statusCode,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        int versionNumber,
        Guid? supersedesConceptId,
        Guid? tenantId,
        bool isSystemDefined,
        Guid ownerUserId,
        Guid businessStewardUserId,
        Guid? technicalStewardUserId,
        string definitionSource,
        string? licensingNotes,
        Guid createdByUserId,
        DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        if (conceptSchemeId == Guid.Empty || ownerUserId == Guid.Empty || businessStewardUserId == Guid.Empty)
            throw new KnowledgeDomainException("Scheme, owner, and business steward identifiers are required.");
        if (parentConceptId == id || supersedesConceptId == id)
            throw new KnowledgeDomainException("A concept cannot parent or supersede itself.");
        if (versionNumber < 1)
            throw new KnowledgeDomainException("VersionNumber must be at least one.");
        KnowledgeGuard.EffectiveDates(effectiveFromUtc, effectiveToUtc);

        ConceptSchemeId = conceptSchemeId;
        ConceptCode = KnowledgeGuard.Code(conceptCode, "ConceptCode", 100);
        ConceptTypeCode = KnowledgeGuard.Code(conceptTypeCode, "ConceptTypeCode", 50);
        PreferredLabel = KnowledgeGuard.Required(preferredLabel, "PreferredLabel", 250);
        Definition = definition?.Trim();
        ParentConceptId = parentConceptId;
        IsAbstract = isAbstract;
        IsSelectable = isSelectable;
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        VersionNumber = versionNumber;
        SupersedesConceptId = supersedesConceptId;
        OwnerUserId = ownerUserId;
        BusinessStewardUserId = businessStewardUserId;
        TechnicalStewardUserId = technicalStewardUserId;
        DefinitionSource = KnowledgeGuard.Required(definitionSource, "DefinitionSource", 500);
        LicensingNotes = licensingNotes?.Trim();
    }

    public Guid ConceptSchemeId { get; }
    public string ConceptCode { get; }
    public string ConceptTypeCode { get; private set; }
    public string PreferredLabel { get; private set; }
    public string? Definition { get; private set; }
    public Guid? ParentConceptId { get; private set; }
    public bool IsAbstract { get; private set; }
    public bool IsSelectable { get; private set; }
    public string StatusCode { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public int VersionNumber { get; }
    public Guid? SupersedesConceptId { get; }
    public Guid OwnerUserId { get; private set; }
    public Guid BusinessStewardUserId { get; private set; }
    public Guid? TechnicalStewardUserId { get; private set; }
    public string DefinitionSource { get; private set; }
    public string? LicensingNotes { get; private set; }
    public IReadOnlyCollection<ConceptLabel> Labels => _labels.AsReadOnly();
    public bool IsPublished => StatusCode.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase);

    public void AddLabel(ConceptLabel label)
    {
        if (label.KnowledgeConceptId != Id)
            throw new KnowledgeDomainException("The label belongs to a different concept.");
        KnowledgeGuard.SameScope(TenantId, label.TenantId, "Concept labels");
        if (_labels.Any(existing => existing.NormalizedLabel == label.NormalizedLabel && existing.LanguageCode == label.LanguageCode && !existing.IsDeprecated))
            throw new KnowledgeDomainException("An active label with the same normalized value and language already exists.");
        if (label.IsPreferred && _labels.Any(existing => existing.IsPreferred && existing.LanguageCode == label.LanguageCode && !existing.IsDeprecated))
            throw new KnowledgeDomainException("Only one active preferred label is allowed per language.");
        _labels.Add(label);
    }

    public void ReviseDraft(
        string conceptTypeCode,
        string preferredLabel,
        string? definition,
        Guid? parentConceptId,
        bool isAbstract,
        bool isSelectable,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        Guid ownerUserId,
        Guid businessStewardUserId,
        Guid? technicalStewardUserId,
        string definitionSource,
        string? licensingNotes,
        Guid actorUserId,
        DateTime modifiedUtc)
    {
        if (IsPublished)
            throw new KnowledgeDomainException("Published concepts are immutable. Create a new version for a material change.");
        if (parentConceptId == Id)
            throw new KnowledgeDomainException("A concept cannot be its own parent.");
        KnowledgeGuard.EffectiveDates(effectiveFromUtc, effectiveToUtc);

        ConceptTypeCode = KnowledgeGuard.Code(conceptTypeCode, "ConceptTypeCode", 50);
        PreferredLabel = KnowledgeGuard.Required(preferredLabel, "PreferredLabel", 250);
        Definition = definition?.Trim();
        ParentConceptId = parentConceptId;
        IsAbstract = isAbstract;
        IsSelectable = isSelectable;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        OwnerUserId = ownerUserId;
        BusinessStewardUserId = businessStewardUserId;
        TechnicalStewardUserId = technicalStewardUserId;
        DefinitionSource = KnowledgeGuard.Required(definitionSource, "DefinitionSource", 500);
        LicensingNotes = licensingNotes?.Trim();
        MarkModified(actorUserId, modifiedUtc);
    }

    public void TransitionTo(string statusCode, Guid actorUserId, DateTime modifiedUtc)
    {
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        MarkModified(actorUserId, modifiedUtc);
    }
}
