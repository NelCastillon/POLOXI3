namespace Legal.Application.Features.Intelligence.Decision;

// ──────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI v4.0 — Proposition Integrity BRIDGE (advisory wiring seam).
//
// Connects the persisted matter evidence graph (LegalMatterEvidenceGraphDto: propositions, evidence
// items, SUPPORTS/CONTRADICTS edges) to the pure v4.0 pillars and runs the §9 Proposition Integrity
// Gate for every proposition. It is the ONLY place graph rows are translated into the pillars' inputs;
// the pillars themselves stay pure and DB-agnostic.
//
// It is advisory/display-only, mirroring MatterPropositionInformationValueService: it never mutates the
// graph, never blocks the authoritative decision, and degrades gracefully (a proposition that cannot be
// evaluated is simply omitted). Deterministic and side-effect-free so it is unit-testable without a DB.
//
// Signal derivation from stored rows (no live LLM proposal available here):
//   • APR — a stored atomic fact proposition is treated as AtomicAccepted; a DISPUTED proposition (open
//     challenge) is left Unresolved. We never fabricate a compound split from stored text.
//   • Light Evidence Graph — each SUPPORTS edge becomes a support binding; the evidence item's passage
//     presence provides the source span; the originating document provides correlation grouping. A
//     CONTRADICTS edge becomes a conflict binding. Entailment is taken as satisfied for an admitted
//     stored support edge (the admission policy already downgraded non-grounding edges to INSUFFICIENT).
//   • Derivation cycles are not expressible in the current support schema, so HasGraphCycle stays false.
// ──────────────────────────────────────────────────────────────────────────────────────────────────

// One proposition's advisory integrity outcome, keyed by the matter fact-proposition id.
public sealed record MatterPropositionIntegrityResult(
    Guid PropositionId,
    LegalIntegrityDisposition Disposition,
    bool StructuralEligible,
    bool EvidenceEligible,
    IReadOnlyList<string> ReasonCodes);

public static class LegalPropositionIntegrityBridge
{
    // Evaluate every proposition in the matter graph through the §9 gate. evidenceDocumentSource maps an
    // evidence item id to its originating document id (used for correlated-source detection) — the same
    // map MatterPropositionInformationValueService already builds for redundancy scoring.
    public static IReadOnlyList<MatterPropositionIntegrityResult> Evaluate(
        IReadOnlyCollection<LegalEvidenceGraphPropositionDto>? propositions,
        IReadOnlyCollection<LegalEvidenceGraphItemDto>? evidence,
        IReadOnlyDictionary<Guid, Guid>? evidenceDocumentSource = null)
    {
        if (propositions is null || propositions.Count == 0)
            return [];

        // Which evidence items carry a traceable source span (a passage / passage text). Absent span ⇒
        // a support edge cannot ground and the Light Evidence Graph reports SOURCE_SPAN_MISSING.
        var spanByEvidenceId = (evidence ?? [])
            .GroupBy(e => e.LegalEvidenceItemId)
            .ToDictionary(
                g => g.Key,
                g => HasSourceSpan(g.First()));

        var results = new List<MatterPropositionIntegrityResult>(propositions.Count);

        foreach (var proposition in propositions)
        {
            var propId = proposition.LegalFactPropositionId.ToString("N");

            var evidenceUnits = new List<LegalEvidenceUnit>();
            var bindings = new List<LegalEvidenceBinding>();

            foreach (var edge in proposition.Support ?? [])
            {
                var evidenceId = edge.LegalEvidenceItemId.ToString("N");
                var hasSpan = spanByEvidenceId.TryGetValue(edge.LegalEvidenceItemId, out var s) && s;
                var sourceDocId = evidenceDocumentSource is not null
                    && evidenceDocumentSource.TryGetValue(edge.LegalEvidenceItemId, out var docId)
                        ? docId.ToString("N")
                        : edge.LegalEvidenceItemId.ToString("N"); // fall back to the item id (distinct source).

                evidenceUnits.Add(new LegalEvidenceUnit(evidenceId, sourceDocId, hasSpan ? "span" : null));

                if (string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase))
                    bindings.Add(new LegalEvidenceBinding(edge.LegalPropositionSupportId.ToString("N"), LegalEvidenceBindingRoles.Supports, evidenceId, propId));
                else if (string.Equals(edge.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase))
                    bindings.Add(new LegalEvidenceBinding(edge.LegalPropositionSupportId.ToString("N"), LegalEvidenceBindingRoles.Conflicts, evidenceId, propId));
            }

            var evidenceProp = new LegalEvidenceProposition(propId);
            var signals = LegalLightEvidenceGraph
                .DeriveSignals([evidenceProp], evidenceUnits, bindings)
                .Single();

            var apr = BuildAprAssessment(proposition.FactStateCode);

            // A binding was proposed but could not ground (missing span / failed entailment). The Light
            // Evidence Graph reports HasSupportingEvidence=false in that case; the gate only raises the
            // admissibility REPAIR when support is claimed, so surface the claim here to trigger it.
            var claimsSupport = signals.HasSupportingEvidence || !signals.HasSourceSpan || !signals.EntailmentSatisfied;

            var gateInput = new LegalPropositionIntegrityInput(
                propId,
                apr,
                HasSupportingEvidence: claimsSupport,
                HasSourceSpan: signals.HasSourceSpan,
                EntailmentSatisfied: signals.EntailmentSatisfied,
                IsCorrelatedOnly: signals.IsCorrelatedOnly);

            var result = LegalPropositionIntegrityGate.Evaluate(gateInput);

            results.Add(new MatterPropositionIntegrityResult(
                proposition.LegalFactPropositionId,
                result.Disposition,
                result.StructuralEligible,
                result.EvidenceEligible,
                result.ReasonCodes));
        }

        return results;
    }

    // Stored propositions arrive already atomized; the APR structural verdict therefore reflects the
    // fact state only — an open dispute stays Unresolved, an invalidated proposition needs repair.
    private static LegalAprAssessment BuildAprAssessment(string? factStateCode)
    {
        if (string.Equals(factStateCode, LegalFactStates.Disputed, StringComparison.OrdinalIgnoreCase))
            return new LegalAprAssessment(
                LegalAprDisposition.Unresolved, LegalPropositionStructuralStates.Uncertain, [LegalAprReasonCodes.OpenChallenge]);

        if (string.Equals(factStateCode, LegalFactStates.Invalidated, StringComparison.OrdinalIgnoreCase))
            return new LegalAprAssessment(
                LegalAprDisposition.RepairRequired, LegalPropositionStructuralStates.RepairRequired, [LegalAprReasonCodes.ParentFidelityFailed]);

        return new LegalAprAssessment(
            LegalAprDisposition.AtomicAccepted, LegalPropositionStructuralStates.AtomicAccepted, [LegalAprReasonCodes.AtomicAccepted]);
    }

    private static bool HasSourceSpan(LegalEvidenceGraphItemDto item)
        => item.LegalDocumentPassageId is not null || !string.IsNullOrWhiteSpace(item.PassageText);
}
