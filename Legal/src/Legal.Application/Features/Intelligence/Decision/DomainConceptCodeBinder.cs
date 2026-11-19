namespace Legal.Application.Features.Intelligence.Decision;

public sealed class DomainConceptCodeBinder
{
    private readonly IReadOnlyList<DecisionDomainConceptDto> _concepts;

    private DomainConceptCodeBinder(IReadOnlyCollection<DecisionDomainConceptDto> concepts)
        => _concepts = concepts.OrderBy(concept => concept.SortOrder).ThenBy(concept => concept.ConceptCode, StringComparer.Ordinal).ToArray();

    public static DomainConceptCodeBinder Create(IReadOnlyCollection<DecisionDomainConceptDto> concepts) => new(concepts);

    public DecisionDomainConceptDto? Bind(string? emittedCode, string? evidenceTypeCode, string? evidenceTypeDimension, string? itemDimension)
    {
        if (_concepts.Count == 0)
            return null;

        var normalizedCode = EvidenceTypeCodeBinder.Normalize(emittedCode ?? string.Empty);
        var exact = _concepts.FirstOrDefault(concept =>
            string.Equals(concept.ConceptCode, emittedCode?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (normalizedCode.Length > 0 && EvidenceTypeCodeBinder.Normalize(concept.ConceptCode) == normalizedCode));
        if (exact is not null)
            return exact;

        // A specific evidence-type prefix can disambiguate multiple concepts sharing a dimension
        // (e.g. MEDICAL_LIEN -> MEDICAL_LIEN_SUBROGATION rather than DAMAGES_QUANTUM).
        var typeCode = EvidenceTypeCodeBinder.Normalize(evidenceTypeCode ?? string.Empty);
        if (typeCode.Length > 0 && evidenceTypeDimension is not null)
        {
            var matches = _concepts.Where(concept => concept.IsFallbackEligible &&
                string.Equals(concept.DimensionCode, evidenceTypeDimension, StringComparison.OrdinalIgnoreCase) &&
                EvidenceTypeCodeBinder.Normalize(concept.ConceptCode).StartsWith(typeCode + "_", StringComparison.Ordinal)).Take(2).ToArray();
            if (matches.Length == 1)
                return matches[0];
        }

        // Prefer the canonical evidence-type dimension; use the model dimension only when the
        // evidence type has no configured dimension. Never guess among unrelated concepts.
        var dimension = evidenceTypeDimension ?? itemDimension;
        if (string.IsNullOrWhiteSpace(dimension))
            return null;

        var candidates = _concepts.Where(concept => concept.IsFallbackEligible &&
            string.Equals(concept.DimensionCode, dimension.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
}