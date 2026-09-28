namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI §9 — Proposition Integrity Gate. NOT another score.
//
// A pure composition seam that folds the already-built §5 APR structural assessment and §6 coverage
// finding, together with per-node evidence / authority / snapshot signals, into ONE bounded disposition:
//
//   PASS               — atomic, faithful, covered, and (where applicable) with verified support.
//   PASS_WITH_UNRESOLVED — structurally sound but an accepted atom is still unsupported/unverified. It
//                          remains visible and evaluable under existing core semantics (missing evidence
//                          is NOT treated as false — §8/T31).
//   REPAIR_REQUIRED    — a known, repairable defect (compound structure, dropped parent qualifier,
//                          binding that fails entailment).
//   BLOCKED            — a mandatory-repair condition that must be resolved before anything proceeds
//                          (security/contract invalidity, graph cycle, stale snapshot, material coverage
//                          gap that blocks the relevant readiness — §9/T36).
//
// It also emits two INDEPENDENT eligibility flags (readiness and scoring never collapse into one opaque
// confidence): StructuralEligible (the node is a validated atomic leaf) and EvidenceEligible (its
// supporting evidence is admissible/verified). Unverified bindings never silently count as verified.
//
// Pure and side-effect-free (mirrors LegalAdaptivePropositionResolver / LegalCoverageValidator) so it is
// unit-testable without a database and adds no second scoring/IV engine. It performs no retrieval and no
// LLM call — a later slice feeds it live snapshot signals.
// ────────────────────────────────────────────────────────────────────────────────────────────────

// §9 four-valued disposition.
public enum LegalIntegrityDisposition
{
    Pass,
    PassWithUnresolved,
    RepairRequired,
    Blocked
}

// §9 reason codes (stable audit strings).
public static class LegalIntegrityReasonCodes
{
    public const string SecurityViolation = "SECURITY_VIOLATION";
    public const string ContractInvalid = "CONTRACT_INVALID";
    public const string GraphCycle = "GRAPH_CYCLE";
    public const string StaleSnapshot = "STALE_SNAPSHOT";
    public const string StructuralCompound = "STRUCTURAL_COMPOUND";
    public const string ParentFidelityFailed = "PARENT_FIDELITY_FAILED";
    public const string StructuralUncertain = "STRUCTURAL_UNCERTAIN";
    public const string MaterialCoverageGap = "MATERIAL_COVERAGE_GAP";
    public const string SourceSpanMissing = "SOURCE_SPAN_MISSING";
    public const string BindingEntailmentFailed = "BINDING_ENTAILMENT_FAILED";
    public const string CorrelatedEvidence = "CORRELATED_EVIDENCE";
    public const string ConditionUnresolved = "CONDITION_UNRESOLVED";
    public const string AuthorityUnverified = "AUTHORITY_UNVERIFIED";
    public const string BudgetExhausted = "BUDGET_EXHAUSTED";
    public const string NoVerifiedSupport = "NO_VERIFIED_SUPPORT";
    public const string IntegrityOk = "INTEGRITY_OK";
}

// The per-node signals the gate composes. The APR assessment (§5) is required; the coverage finding (§6)
// is optional (only meaningful at a scope owner). Evidence signals describe the node's supporting bindings
// as already validated upstream — the gate classifies them, it does not re-derive them.
public sealed record LegalPropositionIntegrityInput(
    string NodeId,
    LegalAprAssessment AprAssessment,
    LegalCoverageFinding? CoverageFinding = null,
    // Mandatory-repair conditions (§9): resolved before anything else proceeds.
    bool SecurityViolation = false,
    bool ContractInvalid = false,
    bool HasGraphCycle = false,
    bool IsSnapshotStale = false,
    // Evidence eligibility signals. HasSupportingEvidence is false for an unsupported accepted atom
    // (visible + evaluable, never "false"). HasSourceSpan/EntailmentSatisfied gate admissibility.
    bool HasSupportingEvidence = false,
    bool HasSourceSpan = true,
    bool EntailmentSatisfied = true,
    bool IsCorrelatedOnly = false,
    // Conditional dependency (§7 CONDITIONAL_ON) that has not yet been resolved.
    bool HasUnresolvedCondition = false,
    // Authority verification (§3 AUTHORITY) — only required for legal propositions.
    bool AuthorityRequired = false,
    bool AuthorityVerified = false);

// §9 result. StructuralEligible and EvidenceEligible are independent — a structurally sound atom can be
// evidence-ineligible and vice versa.
public sealed record LegalIntegrityResult(
    string NodeId,
    LegalIntegrityDisposition Disposition,
    bool StructuralEligible,
    bool EvidenceEligible,
    IReadOnlyList<string> ReasonCodes);

public static class LegalPropositionIntegrityGate
{
    public static LegalIntegrityResult Evaluate(LegalPropositionIntegrityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.AprAssessment);

        var reasons = new List<string>();

        // Independent eligibility flags first (never collapsed into the disposition).
        var structuralEligible = input.AprAssessment.Disposition == LegalAprDisposition.AtomicAccepted;
        var evidenceEligible =
            input.HasSupportingEvidence &&
            input.HasSourceSpan &&
            input.EntailmentSatisfied &&
            !input.IsCorrelatedOnly &&
            (!input.AuthorityRequired || input.AuthorityVerified);

        // 1) Mandatory-repair conditions → BLOCKED (security/contract/cycle/stale). These outrank everything.
        if (input.SecurityViolation) reasons.Add(LegalIntegrityReasonCodes.SecurityViolation);
        if (input.ContractInvalid) reasons.Add(LegalIntegrityReasonCodes.ContractInvalid);
        if (input.HasGraphCycle) reasons.Add(LegalIntegrityReasonCodes.GraphCycle);
        if (input.IsSnapshotStale) reasons.Add(LegalIntegrityReasonCodes.StaleSnapshot);
        if (reasons.Count > 0)
            return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.Blocked, structuralEligible, evidenceEligible, reasons);

        // 2) A material coverage gap blocks the relevant readiness (§9/T36).
        if (input.CoverageFinding is { HasMaterialGap: true })
        {
            reasons.Add(LegalIntegrityReasonCodes.MaterialCoverageGap);
            return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.Blocked, structuralEligible, evidenceEligible, reasons);
        }

        // 3) Structural defects from the APR assessment (§5).
        switch (input.AprAssessment.Disposition)
        {
            case LegalAprDisposition.Compound:
                reasons.Add(LegalIntegrityReasonCodes.StructuralCompound);
                return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.RepairRequired, structuralEligible, evidenceEligible, reasons);
            case LegalAprDisposition.RepairRequired:
                reasons.Add(LegalIntegrityReasonCodes.ParentFidelityFailed);
                return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.RepairRequired, structuralEligible, evidenceEligible, reasons);
            case LegalAprDisposition.Ambiguous:
            case LegalAprDisposition.EvidenceProbe:
                reasons.Add(LegalIntegrityReasonCodes.StructuralUncertain);
                return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.RepairRequired, structuralEligible, evidenceEligible, reasons);
        }

        // 4) Binding admissibility repairs — a claimed support that cannot ground is a repairable defect.
        if (input.HasSupportingEvidence && !input.HasSourceSpan)
        {
            reasons.Add(LegalIntegrityReasonCodes.SourceSpanMissing);
            return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.RepairRequired, structuralEligible, evidenceEligible, reasons);
        }
        if (input.HasSupportingEvidence && !input.EntailmentSatisfied)
        {
            reasons.Add(LegalIntegrityReasonCodes.BindingEntailmentFailed);
            return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.RepairRequired, structuralEligible, evidenceEligible, reasons);
        }

        // 5) Structurally clean atom. Collect any UNRESOLVED signals — these keep the node evaluable rather
        //    than blocking it (missing evidence is not false — §8/T31).
        var unresolved = false;
        if (input.AprAssessment.Disposition == LegalAprDisposition.Unresolved)
        {
            reasons.Add(LegalIntegrityReasonCodes.BudgetExhausted);
            unresolved = true;
        }
        if (!input.HasSupportingEvidence)
        {
            reasons.Add(LegalIntegrityReasonCodes.NoVerifiedSupport);
            unresolved = true;
        }
        if (input.IsCorrelatedOnly)
        {
            reasons.Add(LegalIntegrityReasonCodes.CorrelatedEvidence);
            unresolved = true;
        }
        if (input.HasUnresolvedCondition)
        {
            reasons.Add(LegalIntegrityReasonCodes.ConditionUnresolved);
            unresolved = true;
        }
        if (input.AuthorityRequired && !input.AuthorityVerified)
        {
            reasons.Add(LegalIntegrityReasonCodes.AuthorityUnverified);
            unresolved = true;
        }

        if (unresolved)
            return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.PassWithUnresolved, structuralEligible, evidenceEligible, reasons);

        reasons.Add(LegalIntegrityReasonCodes.IntegrityOk);
        return new LegalIntegrityResult(input.NodeId, LegalIntegrityDisposition.Pass, structuralEligible, evidenceEligible, reasons);
    }

    public static IReadOnlyList<LegalIntegrityResult> EvaluateMany(IEnumerable<LegalPropositionIntegrityInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return inputs.Select(Evaluate).ToList();
    }
}
