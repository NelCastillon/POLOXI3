namespace Legal.Application.Features.Intelligence.Decision;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Duplicate-source / evidence-correlation control (blueprint §"evidence correlation controls").
//
// Corroboration only adds information when it is INDEPENDENT. Several SUPPORTS edges that all trace
// back to the SAME originating document are a single source echoed many times, not independent
// corroboration, and must not inflate a proposition's apparent support. This pure policy measures how
// far a proposition's admitted supporting evidence collapses onto a small number of distinct source
// documents and returns a redundancy penalty in [0,1].
//
//   redundancy = 1 − (distinctSourceDocuments / totalSupportingEdges)
//
//   * 1 supporting edge                    → redundancy 0   (nothing to be redundant against)
//   * N edges from N distinct documents    → redundancy 0   (fully independent corroboration)
//   * N edges from 1 document              → redundancy 1 − 1/N (maximally correlated)
//
// The value is fed into POLOXI's EXISTING Information-Value math (DecisionCoreMath.InformationValue's
// redundancyPenalty term / the proposition IV projection) rather than a new formula. Kept pure and
// side-effect-free (mirrors LegalEvidenceAdmissionPolicy / LegalPropositionBindingPolicy) so it is
// unit-testable without a database.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public static class LegalEvidenceRedundancyPolicy
{
    // Compute the redundancy penalty for a proposition from the source documents backing each of its
    // ADMITTED supporting evidence edges. sourceDocumentIds contains one entry per supporting edge
    // (repeat the same document id when multiple edges cite the same source); edges whose evidence
    // could not be traced to a source document are ignored because they add no corroboration signal.
    public static decimal ComputeRedundancyPenalty(IReadOnlyCollection<Guid?> sourceDocumentIds)
    {
        ArgumentNullException.ThrowIfNull(sourceDocumentIds);

        var traced = sourceDocumentIds.Where(id => id is Guid).Select(id => id!.Value).ToArray();
        var totalEdges = traced.Length;
        if (totalEdges <= 1)
            return 0m;

        var distinctSources = traced.Distinct().Count();
        var penalty = 1m - ((decimal)distinctSources / totalEdges);
        return penalty < 0m ? 0m : penalty > 1m ? 1m : penalty;
    }
}
