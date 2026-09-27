using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity — Matter Change Processor (Phase 1: Change Awareness).
//
// Determines whether newly received information affects an existing decision, WITHOUT rerunning the
// full LLM pipeline. It registers the immutable source version, extracts candidate facts, matches
// them against existing propositions/dependencies, classifies material impact, and — when impact is
// found — marks the affected snapshot's reliance status and creates attorney review tasks.
//
// It never deletes history and never silently reverses an attorney-approved conclusion. Targeted
// reevaluation (reopening POLOXI branches / recompetition) is a later phase that reuses POLOXI Core.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public interface IMatterChangeProcessor
{
    // Processes a single matter change. Idempotent on (tenant, matter, identity key): a retried call
    // for the same source version returns the already-recorded result instead of processing twice.
    Task<MatterChangeProcessingResult> ProcessDocumentChangeAsync(
        Guid tenantId,
        Guid userId,
        Guid decisionMatterId,
        Guid legalDocumentId,
        Guid legalDocumentVersionId,
        string sourceHash,
        string? sourceLabel,
        DateTime? documentDateUtc,
        CancellationToken cancellationToken = default);
}
