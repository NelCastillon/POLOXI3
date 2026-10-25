using System.Text.RegularExpressions;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Domain Pack mode — deterministic L2 eligibility (migration 0445).
//
// Every catalog target receives an explicit disposition BEFORE any LLM review. Deterministic
// exclusions are configuration-driven (incident families, concept jurisdiction, SUPERSEDES
// relations) and are never case-specific. Mandatory decision concepts (IsRequiredCoverage) are never
// excluded here; they are legal-applicability gates, not evidence. Pure and unit-testable.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class DomainPackScopeEligibility
{
    public const string RelationSupersedes = "SUPERSEDES";

    public sealed record Disposition(bool IsEligible, string? ExclusionCode, string? Reason);

    public const string ExclusionIncidentFamily = "INCIDENT_FAMILY_MISMATCH";
    public const string ExclusionJurisdiction = "JURISDICTION_MISMATCH";
    public const string ExclusionSuperseded = "SUPERSEDED_BY_JURISDICTION_RULE";

    private static readonly Disposition Eligible = new(true, null, null);

    // Resolves the incident families the matter belongs to by matching each family's SQL-LIKE style
    // patterns ('%' wildcard, case-insensitive) against the matter signals (matter type, subtype, claim
    // type, PI profile incident type). Empty when the matter carries no recognizable incident signal.
    public static IReadOnlySet<string> ResolveMatterFamilies(IEnumerable<DecisionDomainPackIncidentFamilyDto> families, IEnumerable<string?> matterSignals)
    {
        var signals = matterSignals.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim().ToLowerInvariant()).Distinct().ToArray();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (signals.Length == 0)
            return result;
        foreach (var family in families)
        {
            foreach (var pattern in SplitList(family.MatterSignalPatterns))
            {
                var regex = LikeToRegex(pattern);
                if (signals.Any(regex.IsMatch))
                {
                    result.Add(family.FamilyCode);
                    break;
                }
            }
        }
        return result;
    }

    public const string IncidentFamilyConflict = "INCIDENT_FAMILY_CONFLICT";

    // Outcome of resolving incident families from BOTH the structured matter signals (type / subtype /
    // claim type / PI incident type code) and the free-text incident narrative. When both sides resolve and
    // disagree, the matter is in INCIDENT_FAMILY_CONFLICT: no family-based deterministic exclusion is safe,
    // so the effective families are the UNION (every tagged target stays eligible for LLM review) and the
    // run must surface the conflict for repair. Nothing is silently "chosen".
    public sealed record FamilyScope(IReadOnlySet<string> Structured, IReadOnlySet<string> Narrative, IReadOnlySet<string> Effective, bool IsConflict)
    {
        public string? ConflictReason => IsConflict
            ? $"{IncidentFamilyConflict}: structured matter signals resolve to [{string.Join(", ", Structured.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}] but the incident narrative resolves to [{string.Join(", ", Narrative.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}]; family exclusions suspended (union applied)."
            : null;
    }

    public static FamilyScope ResolveMatterFamilyScope(IEnumerable<DecisionDomainPackIncidentFamilyDto> families, IEnumerable<string?> structuredSignals, IEnumerable<string?> narrativeTexts)
    {
        var list = families as IReadOnlyList<DecisionDomainPackIncidentFamilyDto> ?? families.ToList();
        var structured = ResolveMatterFamilies(list, structuredSignals);
        var narrative = ResolveMatterFamilies(list, narrativeTexts);
        var conflict = structured.Count > 0 && narrative.Count > 0 && !structured.Overlaps(narrative);
        var effective = new HashSet<string>(structured, StringComparer.OrdinalIgnoreCase);
        if (conflict) effective.UnionWith(narrative);
        return new FamilyScope(structured, narrative, effective, conflict);
    }

    // Evidence Type applicability. Untagged types apply everywhere. A tagged type is excluded only when
    // the matter resolved to at least one family and none of the type's families is among them.
    public static Disposition EvaluateEvidenceType(DecisionDomainPackEvidenceTypeDto evidenceType, IReadOnlySet<string> matterFamilies)
    {
        var tagged = SplitList(evidenceType.IncidentFamilyCodes).ToArray();
        if (tagged.Length == 0 || matterFamilies.Count == 0)
            return Eligible;
        if (tagged.Any(matterFamilies.Contains))
            return Eligible;
        return new Disposition(false, ExclusionIncidentFamily,
            $"Evidence type applies to incident families [{string.Join(", ", tagged)}]; matter resolved to [{string.Join(", ", matterFamilies.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}].");
    }

    public const string ExclusionMatterType = "MATTER_TYPE_MISMATCH";

    // Matter-type applicability (migration 0455 — normalized Evidence Type <-> Matter Type junction).
    // Fail-open by design: an evidence type with NO matter-type tags applies to every matter type, and a
    // matter with NO resolved matter-type binding never excludes anything. A tagged type is excluded only
    // when the matter's bound matter-type code is known and not among the type's tags. Required-coverage
    // is enforced by callers before any deterministic exclusion (mirrors incident-family semantics).
    public static Disposition EvaluateEvidenceTypeMatterType(DecisionDomainPackEvidenceTypeDto evidenceType, string? matterTypeCode)
    {
        var tagged = evidenceType.MatterTypeCodeTags;
        if (tagged.Count == 0 || string.IsNullOrWhiteSpace(matterTypeCode))
            return Eligible;
        if (tagged.Any(t => string.Equals(t?.Trim(), matterTypeCode.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Eligible;
        return new Disposition(false, ExclusionMatterType,
            $"Evidence type applies to matter types [{string.Join(", ", tagged)}]; matter is bound to '{matterTypeCode}'.");
    }

    // 0461 broad-type gate: matter.MatterTypeCode -> Legal_MatterSubtype (all subtypes of that broad
    // type) -> junction tags. The junction IS the admission list: an evidence type enters the funnel only
    // if it is tagged to at least one subtype of the matter's broad Matter Type. Untagged evidence types
    // are NOT universal - they are excluded (required-coverage types are still exempted by callers).
    // Fail-open only when the subtype catalog itself is unavailable (empty set).
    public static Disposition EvaluateEvidenceTypeMatterType(
        DecisionDomainPackEvidenceTypeDto evidenceType, string? matterSubtypeCode, IReadOnlySet<string> matterTypeSubtypeCodes)
    {
        if (string.IsNullOrWhiteSpace(matterSubtypeCode) && matterTypeSubtypeCodes.Count == 0)
            return Eligible;
        var tagged = evidenceType.MatterTypeCodeTags;
        if (tagged.Count == 0)
            return new Disposition(false, ExclusionMatterType,
                "Evidence type has no matter-subtype tags; only evidence types tagged to a subtype of the matter's Matter Type enter the funnel.");
        // Bound matter subtype: narrow to that exact subtype's tagged evidence types.
        if (!string.IsNullOrWhiteSpace(matterSubtypeCode))
            return tagged.Any(t => string.Equals(t?.Trim(), matterSubtypeCode.Trim(), StringComparison.OrdinalIgnoreCase))
                ? Eligible
                : new Disposition(false, ExclusionMatterType,
                    $"Evidence type applies to matter subtypes [{string.Join(", ", tagged)}]; matter is bound to '{matterSubtypeCode}'.");
        if (tagged.Any(t => t is not null && matterTypeSubtypeCodes.Contains(t.Trim())))
            return Eligible;
        return new Disposition(false, ExclusionMatterType,
            $"Evidence type applies to matter subtypes [{string.Join(", ", tagged)}]; none belong to the matter's broad Matter Type.");
    }

    // Concept applicability. Required-coverage concepts are mandatory decision gates and are always
    // eligible. A concept carrying a JurisdictionCode is excluded when the matter jurisdiction is known
    // and different. A concept targeted by a SUPERSEDES relation whose source concept is applicable to the
    // matter jurisdiction is excluded (e.g. a generic threshold rule superseded by a pure-comparative rule).
    public static Disposition EvaluateConcept(
        DecisionDomainConceptDto concept,
        string? matterJurisdiction,
        IEnumerable<DecisionDomainConceptRelationDto> relations,
        IReadOnlyDictionary<string, DecisionDomainConceptDto> conceptsByCode)
    {
        if (concept.IsRequiredCoverage)
            return Eligible;
        var jurisdiction = NormalizeJurisdiction(matterJurisdiction);
        if (!string.IsNullOrWhiteSpace(concept.JurisdictionCode) && jurisdiction is not null
            && !JurisdictionMatches(concept.JurisdictionCode, jurisdiction))
            return new Disposition(false, ExclusionJurisdiction,
                $"Concept applies to jurisdiction {concept.JurisdictionCode}; matter jurisdiction is {matterJurisdiction}.");
        if (jurisdiction is not null)
        {
            foreach (var relation in relations)
            {
                if (!string.Equals(relation.RelationTypeCode, RelationSupersedes, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(relation.TargetConceptCode, concept.ConceptCode, StringComparison.OrdinalIgnoreCase)) continue;
                var relationJurisdiction = relation.JurisdictionCode;
                if (string.IsNullOrWhiteSpace(relationJurisdiction) && conceptsByCode.TryGetValue(relation.SourceConceptCode, out var source))
                    relationJurisdiction = source.JurisdictionCode;
                if (!string.IsNullOrWhiteSpace(relationJurisdiction) && JurisdictionMatches(relationJurisdiction, jurisdiction))
                    return new Disposition(false, ExclusionSuperseded,
                        $"Superseded by {relation.SourceConceptCode} for jurisdiction {relationJurisdiction}.");
            }
        }
        return Eligible;
    }

    public static bool JurisdictionMatches(string? conceptJurisdiction, string? matterJurisdiction)
    {
        var a = NormalizeJurisdiction(conceptJurisdiction);
        var b = NormalizeJurisdiction(matterJurisdiction);
        if (a is null || b is null) return false;
        return a == b || a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }

    // "California", "CA - Los Angeles County", "CALIFORNIA" → "CALIFORNIA"; "CA" alone → "CA".
    private static string? NormalizeJurisdiction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var token = Regex.Replace(value.Trim().ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');
        return token.Length == 0 ? null : token;
    }

    private static IEnumerable<string> SplitList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(v => v.Length > 0);

    private static Regex LikeToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern.Trim().ToLowerInvariant()).Replace("%", ".*", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.CultureInvariant);
    }
}
