namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI §6 — MECE coverage validator (scope-aware, NOT forced exclusivity).
//
// Validates the horizontal completeness of a set of sibling items (L1 competing outcomes, L2 material
// factors, or L3+ split children) under a domain-configured coverage policy. It never invents a
// "child-count score" and never certifies exhaustiveness on its own (a silent LLM cannot certify
// completeness). It only reports:
//   • material gaps  — a REQUIRED item under the active policy is absent (blocks the relevant readiness),
//   • coverage debt  — items an open-world scope has not yet excluded (advisory, non-blocking),
//   • overlaps       — classified, not auto-rejected, except under an EXCLUSIVE_PARTITION policy where a
//                      declared overlap is itself a material defect.
//
// Coexisting remedies are allowed: NON_EXCLUSIVE_SET / CONDITIONAL_ALTERNATIVES do not force competing
// dispositions into mutual exclusivity (§6, T10). Overlap is surfaced for review (T11); a missing
// required component is a material gap (T09).
//
// Pure and side-effect-free (mirrors LegalAdaptivePropositionResolver / LegalEvidenceAdmissionPolicy /
// LegalPropositionBindingPolicy) so it is unit-testable without a database and adds no second scoring
// engine. Maps onto existing hierarchy shapes; introduces no new persistence.
// ────────────────────────────────────────────────────────────────────────────────────────────────

// §6 domain-configured coverage policies.
public static class LegalCoveragePolicies
{
    // Mutually exclusive AND collectively exhaustive: every item excludes the others (classic MECE).
    public const string ExclusivePartition = "EXCLUSIVE_PARTITION";
    // Items may coexist; no exclusivity is required (e.g. coexisting remedies).
    public const string NonExclusiveSet = "NON_EXCLUSIVE_SET";
    // Alternatives that apply under differing conditions; coexistence is condition-gated, not forbidden.
    public const string ConditionalAlternatives = "CONDITIONAL_ALTERNATIVES";
    // A fixed set of required components must all be present (e.g. the elements of a cause of action).
    public const string RequiredComponents = "REQUIRED_COMPONENTS";
    // Exhaustiveness cannot be justified: absence is coverage debt, never a hard gap. Default.
    public const string OpenWorld = "OPEN_WORLD";

    public static bool IsValid(string? policy) =>
        policy is ExclusivePartition or NonExclusiveSet or ConditionalAlternatives or RequiredComponents or OpenWorld;
}

// The scope a coverage check runs over.
public static class LegalCoverageScopes
{
    public const string L1Outcomes = "L1_OUTCOMES";
    public const string L2Factors = "L2_FACTORS";
    public const string L3Split = "L3_SPLIT";
}

// Classification of an overlap between two coexisting items (§6 overlap review).
public static class LegalCoverageOverlapKinds
{
    // Legitimate shared content under a non-exclusive/conditional policy — recorded, not a defect.
    public const string PermittedCoexistence = "PERMITTED_COEXISTENCE";
    // Overlap that violates an EXCLUSIVE_PARTITION policy — a material partition defect.
    public const string PartitionViolation = "PARTITION_VIOLATION";
}

// One sibling item presented to the coverage check. IsRepresented is false when the policy expects the
// item (by RequiredCode or domain configuration) but the hierarchy does not contain it. OverlapsWith
// lists the codes this item shares material content with (declared upstream, e.g. by the LLM or domain
// relations) — the validator classifies these, it does not infer new ones.
public sealed record LegalCoverageItem(
    string ItemCode,
    string Label,
    bool IsRequired,
    bool IsRepresented,
    IReadOnlyCollection<string>? OverlapsWith = null,
    bool IsExplicitlyExcluded = false);

// A coverage gap. Material gaps block the relevant readiness; non-material gaps are advisory debt.
public sealed record LegalCoverageGap(string ItemCode, bool IsMaterial, string ReasonCode, string Detail);

// A classified overlap between two coexisting items.
public sealed record LegalCoverageOverlap(string FromItemCode, string ToItemCode, string OverlapKind);

// Reason codes for gaps.
public static class LegalCoverageReasonCodes
{
    public const string RequiredItemMissing = "REQUIRED_ITEM_MISSING";
    public const string RequiredComponentMissing = "REQUIRED_COMPONENT_MISSING";
    public const string PartitionOverlap = "PARTITION_OVERLAP";
    public const string OpenWorldDebt = "OPEN_WORLD_DEBT";
}

// The result of a scope coverage check (§6 CoverageFinding).
public sealed record LegalCoverageFinding(
    string ScopeCode,
    string PolicyCode,
    IReadOnlyList<string> RepresentedItemCodes,
    IReadOnlyList<LegalCoverageGap> Gaps,
    IReadOnlyList<LegalCoverageOverlap> Overlaps)
{
    // Only material gaps block readiness; open-world debt and permitted coexistence never do.
    public bool HasMaterialGap => Gaps.Any(g => g.IsMaterial);
}

public static class LegalCoverageValidator
{
    // Validate horizontal coverage of one scope. expectedRequiredCodes are items the domain policy says
    // must be present regardless of an item's own IsRequired flag (e.g. a defense that must be considered);
    // pass an empty set when the required set is carried on the items themselves.
    public static LegalCoverageFinding Check(
        string scopeCode,
        string policyCode,
        IReadOnlyCollection<LegalCoverageItem> items,
        IReadOnlyCollection<string>? expectedRequiredCodes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeCode);
        ArgumentNullException.ThrowIfNull(items);
        var policy = LegalCoveragePolicies.IsValid(policyCode) ? policyCode : LegalCoveragePolicies.OpenWorld;
        var required = expectedRequiredCodes is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(expectedRequiredCodes, StringComparer.OrdinalIgnoreCase);

        var representedCodes = items
            .Where(i => i.IsRepresented)
            .Select(i => i.ItemCode)
            .ToList();
        var representedSet = new HashSet<string>(representedCodes, StringComparer.OrdinalIgnoreCase);

        var gaps = new List<LegalCoverageGap>();

        // 1) Required items — a required item that is neither represented nor explicitly excluded is a
        //    MATERIAL gap (T09 missing defense). This applies under any policy that carries requirements.
        var requiredComponentsPolicy = string.Equals(policy, LegalCoveragePolicies.RequiredComponents, StringComparison.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var isRequired = item.IsRequired || required.Contains(item.ItemCode);
            if (isRequired && !item.IsRepresented && !item.IsExplicitlyExcluded)
            {
                var reason = requiredComponentsPolicy
                    ? LegalCoverageReasonCodes.RequiredComponentMissing
                    : LegalCoverageReasonCodes.RequiredItemMissing;
                gaps.Add(new LegalCoverageGap(item.ItemCode, IsMaterial: true, reason,
                    $"Required {ScopeNoun(scopeCode)} '{item.Label}' is neither represented nor explicitly excluded."));
            }
        }

        // Required codes that were named upstream but have no item at all in this scope.
        foreach (var code in required)
        {
            if (!representedSet.Contains(code) && items.All(i => !string.Equals(i.ItemCode, code, StringComparison.OrdinalIgnoreCase)))
            {
                var reason = requiredComponentsPolicy
                    ? LegalCoverageReasonCodes.RequiredComponentMissing
                    : LegalCoverageReasonCodes.RequiredItemMissing;
                gaps.Add(new LegalCoverageGap(code, IsMaterial: true, reason,
                    $"Required {ScopeNoun(scopeCode)} '{code}' is absent from the hierarchy."));
            }
        }

        // 2) Overlaps — classify declared overlaps. Under EXCLUSIVE_PARTITION any overlap is a partition
        //    violation (material). Under every other policy coexistence is permitted and only recorded so a
        //    reviewer can see it (T10 coexisting remedies allowed; T11 overlap classified).
        var overlaps = new List<LegalCoverageOverlap>();
        var exclusive = string.Equals(policy, LegalCoveragePolicies.ExclusivePartition, StringComparison.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item.OverlapsWith is null)
                continue;
            foreach (var other in item.OverlapsWith)
            {
                if (string.Equals(item.ItemCode, other, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (exclusive)
                {
                    overlaps.Add(new LegalCoverageOverlap(item.ItemCode, other, LegalCoverageOverlapKinds.PartitionViolation));
                    gaps.Add(new LegalCoverageGap(item.ItemCode, IsMaterial: true, LegalCoverageReasonCodes.PartitionOverlap,
                        $"'{item.Label}' overlaps '{other}' under an exclusive-partition policy."));
                }
                else
                {
                    overlaps.Add(new LegalCoverageOverlap(item.ItemCode, other, LegalCoverageOverlapKinds.PermittedCoexistence));
                }
            }
        }

        return new LegalCoverageFinding(scopeCode, policy, representedCodes, gaps, overlaps);
    }

    private static string ScopeNoun(string scopeCode) => scopeCode switch
    {
        LegalCoverageScopes.L1Outcomes => "outcome",
        LegalCoverageScopes.L2Factors => "factor",
        LegalCoverageScopes.L3Split => "split child",
        _ => "item"
    };
}
