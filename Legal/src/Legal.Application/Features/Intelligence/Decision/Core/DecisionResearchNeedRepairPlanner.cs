namespace Legal.Application.Features.Intelligence.Decision.Core;

// The bounded, diagnosed recovery action the producer should take when the Researchability Gate rejects a
// research-need proposal. This mirrors the Proposal Integrity pattern: POLOXI never weakens the gate to get
// research running; it diagnoses WHY the proposal failed and asks the producer for a targeted, bounded fix.
public enum DecisionResearchNeedDisposition
{
    // Malformed-but-close proposition (e.g. a question instead of a declarative statement) — one bounded,
    // defect-targeted LLM repair that preserves valid leaves.
    Repair,

    // The researchable leaves are valid, but the derived/application (synthesis) leaf is defective (e.g.
    // missing its explicit research question or dependency references). Repair ONLY that application leaf,
    // preserving the accepted LEGAL_RULE/MATTER_FACT leaves — never re-route it to external retrieval.
    RepairApplication,

    // A frontier item that conflates legal rule/authority with matter fact/evidence/application — the
    // producer must decompose it into atomic leaves with correct SourceClass routing.
    Decompose,

    // A leaf routed to public legal research that is actually a matter-specific finding (or otherwise
    // carries the wrong source class) — it must be re-routed to the matter corpus (MATTER_DOCUMENT).
    RouteMatter,

    // Structural failure with no safe automated recovery (no leaves, no source-resolvable leaves after
    // repair) — stop without polluting decision state.
    Unresolved,
}

// Deterministic, side-effect-free planner that maps Researchability Gate defects onto a single bounded
// recovery action and the disposition-specific directive appended to the producer prompt. Extracted from the
// orchestration service so the disposition policy is independently unit-testable. The gate is never weakened
// here: unknown defects fall through to REPAIR (the safe, pre-existing behavior), never to ACCEPT.
public static class DecisionResearchNeedRepairPlanner
{
    // Defects that indicate the frontier proposition conflates legal + matter/application content and must be
    // decomposed into atomic leaves rather than merely reworded.
    private static readonly string[] DecomposeDefectSuffixes =
    [
        "MIXED_LEAF_MUST_BE_DECOMPOSED",
        "RESEARCH_NEED_TYPE_INVALID",
        "ASSUMED_APPLICATION_CONCLUSION",
        "APPLICATION_NODE_MISSING",
        "SEMANTIC_HIERARCHY_TOO_SHALLOW",
        "DERIVED_NODE_MUST_NOT_BE_RETRIEVED",
        "DERIVED_NODE_DEPENDENCIES_INSUFFICIENT",
        "APPLICATION_MUST_BE_DEFERRED",
        "DERIVED_NODE_MUST_NOT_HAVE_RETRIEVAL_INSTRUCTIONS",
    ];

    // Defects that indicate a leaf was routed to public legal research but describes a matter-specific finding,
    // or otherwise has the wrong source class — it must be re-routed to the matter corpus.
    private static readonly string[] RouteMatterDefectSuffixes =
    [
        "MATTER_FINDING_NOT_PUBLICLY_RESEARCHABLE",
        "SOURCE_CLASS_MISMATCH",
        "MATTER_LEAF_MUST_NOT_HAVE_AUTHORITY_KINDS",
    ];

    // Structural defects that repair cannot safely resolve; these end the bounded recovery as UNRESOLVED rather
    // than risk an accept-to-run shortcut.
    private static readonly string[] UnresolvedDefectSuffixes =
    [
        "NO_RESEARCH_LEAVES",
        "NO_SOURCE_RESOLVABLE_LEAVES",
        "INSUFFICIENT_SOURCE_RESOLVABLE_LEAVES",
    ];

    // Diagnoses the bounded recovery action for a set of gate defects. Precedence is deliberately conservative:
    // DECOMPOSE and ROUTE_MATTER (semantic-contract violations) win over a plain REPAIR, and UNRESOLVED only
    // wins when EVERY defect is structural-unrecoverable (nothing a targeted repair can fix).
    public static DecisionResearchNeedDisposition Diagnose(IReadOnlyList<string> defects)
    {
        if (defects is null || defects.Count == 0)
            return DecisionResearchNeedDisposition.Repair;

        if (defects.Any(defect => MatchesAny(defect, DecomposeDefectSuffixes)))
            return DecisionResearchNeedDisposition.Decompose;
        if (defects.Any(defect => MatchesAny(defect, RouteMatterDefectSuffixes)))
            return DecisionResearchNeedDisposition.RouteMatter;
        if (defects.All(defect => MatchesAny(defect, UnresolvedDefectSuffixes)))
            return DecisionResearchNeedDisposition.Unresolved;

        return DecisionResearchNeedDisposition.Repair;
    }

    // Result-aware diagnosis. When the gate reports that valid researchable leaves exist and every residual
    // defect is confined to the derived/application synthesis leaf, the bounded recovery is a TARGETED
    // application repair — the accepted authority/matter leaves are preserved and never re-derived. This
    // does not weaken the gate: it only narrows the repair to the single defective synthesis leaf.
    public static DecisionResearchNeedDisposition Diagnose(DecisionResearchabilityResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (evaluation.CanProgressWithResearchableLeaves)
            return DecisionResearchNeedDisposition.RepairApplication;
        return Diagnose(evaluation.Defects);
    }

    // Builds the disposition-specific directive appended to the producer prompt for the single bounded repair
    // attempt. The gate is never weakened; the producer is told precisely how to make the proposal researchable
    // (reword, decompose, or re-route), preserving valid leaves.
    public static string BuildDirective(DecisionResearchNeedDisposition disposition, IReadOnlyList<string> defects)
    {
        var defectList = "\n- " + string.Join("\n- ", defects ?? []);
        return disposition switch
        {
            DecisionResearchNeedDisposition.RepairApplication =>
                "\n\nThe researchable LEGAL_RULE and MATTER_FACT leaves are ACCEPTED and MUST be preserved verbatim — do not modify, re-route, or remove them. "
                + "ONLY the APPLICATION (synthesis) leaf is defective. Repair that single leaf so that it:\n"
                + "- Provides an explicit ResearchQuestion phrased as a dependent legal-synthesis question (e.g. \"Given the established legal requirements and the verified facts, which elements remain disputed for the requested determination?\").\n"
                + "- Keeps ResearchNeedType APPLICATION, SourceClass NONE, Researchable=false, ApplicationDeferred=true (it is synthesis, NOT an external authority-retrieval request).\n"
                + "- Lists in Requires the research keys of the LEGAL_RULE and MATTER_FACT leaves it depends on (at least two).\n"
                + "- Carries NO SearchQuery, SearchConcepts, or AuthorityKinds.\n"
                + "Do NOT invent evidence or a conclusion for the application; if its dependencies are unresolved it stays unresolved. Fix ONLY these defects:" + defectList
                + "\nReturn the complete corrected JSON object once.",
            DecisionResearchNeedDisposition.Decompose =>
                "\n\nThe frontier target is NOT directly researchable because it conflates legal and matter/application content. "
                + "DECOMPOSE it into atomic leaves, each with the correct ResearchNeedType and SourceClass:\n"
                + "- LEGAL_RULE / LEGAL_AUTHORITY / PROCEDURAL_STANDARD -> SourceClass LEGAL_AUTHORITY (public legal research).\n"
                + "- MATTER_FACT / MATTER_EVIDENCE -> SourceClass MATTER_DOCUMENT (matter corpus, not public research).\n"
                + "- APPLICATION -> SourceClass NONE, Researchable=false, ApplicationDeferred=true, Requires the keys of the rule + fact leaves.\n"
                + "Each researchable leaf's Proposition MUST be a declarative, verifiable statement (never a question). "
                + "Preserve any already-valid leaves. Fix ONLY these defects:" + defectList
                + "\nReturn the complete corrected JSON object once.",
            DecisionResearchNeedDisposition.RouteMatter =>
                "\n\nOne or more leaves are routed to public legal research but describe a MATTER-SPECIFIC finding, or carry the wrong SourceClass. "
                + "RE-ROUTE matter-specific findings to MATTER_FACT/MATTER_EVIDENCE with SourceClass MATTER_DOCUMENT (no AuthorityKinds), "
                + "and keep genuine legal propositions as LEGAL_RULE/LEGAL_AUTHORITY with SourceClass LEGAL_AUTHORITY. "
                + "Preserve valid leaves. Fix ONLY these defects:" + defectList
                + "\nReturn the complete corrected JSON object once.",
            _ =>
                "\n\nREPAIR ONLY THESE DEFECTS:" + defectList
                + "\nEach researchable Proposition MUST be declarative and verifiable (never a question). "
                + "Preserve valid leaves. Return the complete corrected JSON object once.",
        };
    }

    private static bool MatchesAny(string defect, string[] suffixes) =>
        suffixes.Any(suffix => defect.EndsWith(suffix, StringComparison.Ordinal));
}
