using System.Text.RegularExpressions;

namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI APR (Adaptive Proposition Resolution) — §5 deterministic atomicity & parent-fidelity validator.
//
// The LLM PROPOSES whether a proposition is atomic and, when compound, proposes its minimal material
// children. POLOXI does NOT trust that proposal blindly: this pure policy independently validates
// atomicity, parent fidelity, material split, and evidence-probe exclusion, then folds those checks and
// a caller-supplied budget/challenge signal into a bounded §5 disposition.
//
//   AtomicAccepted(p) = LLMProposedAtomic(p)
//                       AND ContextSufficient(p)
//                       AND ParentFidelity(p)
//                       AND NoKnownMaterialSplit(p)
//                       AND NoOpenChallenge(p)
//
// It is intentionally conservative: authoritative decomposition still comes from the LLM. The heuristics
// here only catch KNOWN material splits (co-ordinated independently-evaluable claims) and KNOWN fidelity
// loss (dropped actor/negation/exception/condition/timeframe qualifiers). Anything uncertain is surfaced
// as AMBIGUOUS/UNCERTAIN rather than silently accepted or split.
//
// Pure and side-effect-free (mirrors LegalEvidenceAdmissionPolicy / LegalPropositionBindingPolicy /
// LegalEvidenceRedundancyPolicy / MatterPropositionClaimProjection) so it is unit-testable without a
// database and adds no second scoring/IV engine. Maps onto the existing LegalFactProposition text/state
// shape; introduces no new persistence.
// ────────────────────────────────────────────────────────────────────────────────────────────────

// §3 Structural state dimension — independent of Evidence/Coverage/Decision states.
public static class LegalPropositionStructuralStates
{
    public const string Proposed = "PROPOSED";
    public const string Compound = "COMPOUND";
    public const string AtomicAccepted = "ATOMIC_ACCEPTED";
    public const string Uncertain = "UNCERTAIN";
    public const string RepairRequired = "REPAIR_REQUIRED";
    public const string Reopened = "REOPENED";
    public const string Superseded = "SUPERSEDED";
    public const string Invalid = "INVALID";
}

// §5 APR disposition of a single assess/decompose decision.
public enum LegalAprDisposition
{
    // LLM proposed atomic and every independent validation agreed → accept as an atomic leaf.
    AtomicAccepted,
    // A known material split exists → the node must decompose into minimal validated children.
    Compound,
    // Cannot confidently decide atomicity/fidelity → clarify before accepting or splitting.
    Ambiguous,
    // Proposed children lose the parent's material content (dropped qualifier/negation/exception) → repair.
    RepairRequired,
    // A duplicate of an existing canonical proposition → reuse canonical identity, do not re-register.
    Duplicate,
    // Work item is an investigation probe, not a hierarchy child → exclude from the proposition tree.
    EvidenceProbe,
    // Budget exhausted or an open challenge remains → leave explicitly unresolved (never silently atomic).
    Unresolved
}

// Reason codes accompanying a disposition (audit-friendly, stable strings).
public static class LegalAprReasonCodes
{
    public const string LlmProposedCompound = "LLM_PROPOSED_COMPOUND";
    public const string MaterialSplitDetected = "MATERIAL_SPLIT_DETECTED";
    public const string ParentFidelityFailed = "PARENT_FIDELITY_FAILED";
    public const string EvidenceProbeNotChild = "EVIDENCE_PROBE_NOT_CHILD";
    public const string DuplicateCanonical = "DUPLICATE_CANONICAL";
    public const string BudgetExhausted = "BUDGET_EXHAUSTED";
    public const string OpenChallenge = "OPEN_CHALLENGE";
    public const string ContextInsufficient = "CONTEXT_INSUFFICIENT";
    public const string AtomicAccepted = "ATOMIC_ACCEPTED";
}

// A storage-neutral view of the proposition the LLM assessed, plus what the LLM proposed for it.
public sealed record LegalAprAssessmentInput(
    string PropositionText,
    bool LlmProposedAtomic,
    IReadOnlyList<string> ProposedChildTexts,
    bool ContextSufficient = true,
    bool HasOpenChallenge = false,
    bool IsDuplicateOfCanonical = false);

// Result of an APR assessment.
public sealed record LegalAprAssessment(
    LegalAprDisposition Disposition,
    string StructuralStateCode,
    IReadOnlyList<string> ReasonCodes)
{
    public bool IsAtomicAccepted => Disposition == LegalAprDisposition.AtomicAccepted;
}

public static class LegalAdaptivePropositionResolver
{
    // Coordinating conjunctions that, when joining two independently-evaluable clauses, signal a material
    // split ("driver was speeding AND ran the red light"). Conservative: we only treat as a split when both
    // sides contain a verb-like predicate token, so noun conjunctions ("cars and trucks") do not trip it.
    private static readonly string[] SplitConjunctions = [" and ", " as well as ", "; "];

    // Qualifier tokens whose presence in a parent must be preserved by at least one child. Dropping them
    // changes the material meaning (negation, exception, condition, actor/timeframe scoping).
    private static readonly string[] MaterialQualifiers =
    [
        "not", "no ", "never", "except", "unless", "only if", "provided that", "if ", "when ",
        "before", "after", "while", "due to", "because", "caused by"
    ];

    // Evidence-probe lead-ins: an investigation work item ("obtain the phone records", "verify the
    // timestamp") is NOT a proposition and must never become a hierarchy child (§3, T08).
    private static readonly string[] EvidenceProbeVerbs =
    [
        "obtain", "retrieve", "request", "subpoena", "verify", "confirm", "check", "measure",
        "inspect", "collect", "gather", "review the", "pull the", "look up", "find out"
    ];

    private static readonly Regex PredicateHint =
        new(@"\b(was|were|is|are|did|had|has|ran|drove|caused|failed|breached|owed|used|struck|violated|exceeded)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // True when the text reads as an investigation work item rather than an assertable claim (T08).
    public static bool IsEvidenceProbe(string? propositionText)
    {
        if (string.IsNullOrWhiteSpace(propositionText))
            return false;
        var text = propositionText.TrimStart();
        foreach (var verb in EvidenceProbeVerbs)
        {
            if (text.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Detect a KNOWN material split: two independently-evaluable predicated clauses joined by a
    // coordinating conjunction. Conservative — returns false when either side lacks a predicate hint so a
    // single qualified clause ("driver was speeding before the intersection") is not falsely split (T04).
    public static bool HasKnownMaterialSplit(string? propositionText)
    {
        if (string.IsNullOrWhiteSpace(propositionText))
            return false;
        var text = propositionText;
        foreach (var conjunction in SplitConjunctions)
        {
            var index = text.IndexOf(conjunction, StringComparison.OrdinalIgnoreCase);
            if (index <= 0)
                continue;
            var left = text[..index];
            var right = text[(index + conjunction.Length)..];
            if (PredicateHint.IsMatch(left) && PredicateHint.IsMatch(right))
                return true;
        }
        return false;
    }

    // Parent fidelity: every material qualifier present in the parent must survive in the union of the
    // proposed children (T05, T06). Returns false when a negation/exception/condition/actor qualifier is
    // dropped by all children.
    public static bool ValidateParentFidelity(string parentText, IReadOnlyList<string> childTexts)
    {
        ArgumentNullException.ThrowIfNull(parentText);
        ArgumentNullException.ThrowIfNull(childTexts);
        if (childTexts.Count == 0)
            return true; // nothing decomposed → fidelity is vacuously preserved

        var combinedChildren = string.Join(" \u2016 ", childTexts);
        foreach (var qualifier in MaterialQualifiers)
        {
            if (ContainsToken(parentText, qualifier) && !ContainsToken(combinedChildren, qualifier))
                return false;
        }
        return true;
    }

    // §5 core: fold the LLM proposal + independent validations + budget/challenge signal into one bounded
    // disposition and its resulting §3 structural state.
    public static LegalAprAssessment Assess(LegalAprAssessmentInput input, bool budgetExhausted = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        var reasons = new List<string>();

        // Evidence probes are never hierarchy children — check before anything else (T08).
        if (IsEvidenceProbe(input.PropositionText))
        {
            reasons.Add(LegalAprReasonCodes.EvidenceProbeNotChild);
            return new LegalAprAssessment(LegalAprDisposition.EvidenceProbe, LegalPropositionStructuralStates.Invalid, reasons);
        }

        // Canonical duplicate → reuse existing identity (T07).
        if (input.IsDuplicateOfCanonical)
        {
            reasons.Add(LegalAprReasonCodes.DuplicateCanonical);
            return new LegalAprAssessment(LegalAprDisposition.Duplicate, LegalPropositionStructuralStates.Superseded, reasons);
        }

        var childTexts = input.ProposedChildTexts ?? [];
        var knownMaterialSplit = HasKnownMaterialSplit(input.PropositionText);

        // Compound path: the LLM proposed children OR POLOXI independently detected a material split (T02).
        if (!input.LlmProposedAtomic || childTexts.Count > 0 || knownMaterialSplit)
        {
            if (!input.LlmProposedAtomic || childTexts.Count > 0)
                reasons.Add(LegalAprReasonCodes.LlmProposedCompound);
            if (knownMaterialSplit)
                reasons.Add(LegalAprReasonCodes.MaterialSplitDetected);

            // Validate parent fidelity of any proposed children; dropped qualifiers → repair (T05, T06).
            if (childTexts.Count > 0 && !ValidateParentFidelity(input.PropositionText, childTexts))
            {
                reasons.Add(LegalAprReasonCodes.ParentFidelityFailed);
                return new LegalAprAssessment(LegalAprDisposition.RepairRequired, LegalPropositionStructuralStates.RepairRequired, reasons);
            }

            return new LegalAprAssessment(LegalAprDisposition.Compound, LegalPropositionStructuralStates.Compound, reasons);
        }

        // From here the LLM proposed atomic with no children and no detected split. Apply the remaining
        // AtomicAccepted conjuncts; any failure yields an explicit non-atomic disposition (never silent).
        if (!input.ContextSufficient)
        {
            reasons.Add(LegalAprReasonCodes.ContextInsufficient);
            return new LegalAprAssessment(LegalAprDisposition.Ambiguous, LegalPropositionStructuralStates.Uncertain, reasons);
        }
        if (input.HasOpenChallenge)
        {
            reasons.Add(LegalAprReasonCodes.OpenChallenge);
            return new LegalAprAssessment(LegalAprDisposition.Unresolved, LegalPropositionStructuralStates.Uncertain, reasons);
        }
        if (budgetExhausted)
        {
            // Deferred low-value work stays UNCERTAIN/unresolved — a known-open node is never marked atomic (T12).
            reasons.Add(LegalAprReasonCodes.BudgetExhausted);
            return new LegalAprAssessment(LegalAprDisposition.Unresolved, LegalPropositionStructuralStates.Uncertain, reasons);
        }

        reasons.Add(LegalAprReasonCodes.AtomicAccepted);
        return new LegalAprAssessment(LegalAprDisposition.AtomicAccepted, LegalPropositionStructuralStates.AtomicAccepted, reasons);
    }

    // Whole-word / token-boundary containment so "not" does not match "notice" and "if " does not match
    // "sheriff". Punctuation and the child separator count as boundaries.
    private static bool ContainsToken(string haystack, string token)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(token))
            return false;

        var trimmed = token.Trim();
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(trimmed)}(?![\p{{L}}\p{{N}}])";
        return Regex.IsMatch(haystack, pattern, RegexOptions.IgnoreCase);
    }
}
