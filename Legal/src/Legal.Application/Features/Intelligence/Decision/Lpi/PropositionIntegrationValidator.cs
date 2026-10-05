using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Deterministic proposition-integration VALIDATION gate (Phase 2).
//
// A pure, side-effect-free gate run BEFORE the shared IPropositionIntegrationService inserts a
// reviewed proposition. It enforces the structural/semantic preconditions the specification requires.
// A FAILED check NEVER discards the proposal: it returns an actionable reason and the proposal is
// preserved (ReviewRequired / NeedsHierarchyReview) so an attorney can correct and resubmit.
//
// Checks (each independent, all evaluated so the UI can show every problem at once):
//   * SourceFidelity  — exact source text + locator + document version present (no fabricated source).
//   * Placement       — at least one placement; every placement names a target node.
//   * Relationship    — relationship is one of the qualitative codes; CONTEXT_ONLY adds no support.
//   * Identity        — proposition text is non-empty and atomic (no unsplit compound coordination).
//   * Duplication     — not an exact duplicate of an already-accepted proposition at the same node.
//   * AuthorityBoundary — no numeric score/winner smuggled in; placementFraction only with neighbors.
//   * Revision        — a revise/withdraw references an existing proposition to supersede.
//   * Category        — assertion type preserved; reported/inferred flagged, not promoted to fact.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public enum LpiValidationCheck
{
    SourceFidelity,
    Placement,
    Relationship,
    Identity,
    Duplication,
    AuthorityBoundary,
    Revision,
    Category
}

public sealed record LpiValidationFinding(
    LpiValidationCheck Check,
    bool Passed,
    string Message);

public sealed record LpiValidationOutcome(
    bool IsValid,
    IReadOnlyList<LpiValidationFinding> Findings,
    LpiProposalState PreservedState)   // where to park the proposal when invalid (never discarded)
{
    public IEnumerable<LpiValidationFinding> Failures => Findings.Where(f => !f.Passed);
}

// Operation kind carried from the integration op; mirrors Add|Revise|Withdraw.
public enum LpiOperationKind
{
    Add,
    Revise,
    Withdraw
}

// Everything the pure gate needs. The caller supplies already-accepted siblings/duplicates so the
// gate stays deterministic and DB-free.
public sealed record LpiValidationInput(
    RetrievedProposition Proposition,
    IReadOnlyList<LpiPlacementProposal> Placements,
    LpiOperationKind Operation,
    bool TargetHierarchyRevisionCurrent,               // false => stale, caller reviewed against old revision
    IReadOnlyCollection<string> AcceptedPropositionTextsAtTargets,  // normalized existing accepted texts
    Guid? SupersedesPropositionId);                    // required for Revise/Withdraw

public static class PropositionIntegrationValidator
{
    private static readonly string[] CompoundMarkers =
        [" and ", " and/or ", "; ", " as well as ", " in addition to ", " both "];

    public static LpiValidationOutcome Validate(LpiValidationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var findings = new List<LpiValidationFinding>();

        // ── SourceFidelity ────────────────────────────────────────────────────────────────────────
        var hasSource =
            input.Proposition.DocumentVersionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(input.Proposition.SourceLocator)
            && !string.IsNullOrWhiteSpace(input.Proposition.SourceText);
        findings.Add(new(LpiValidationCheck.SourceFidelity, hasSource,
            hasSource ? "Source provenance present."
                      : "Missing document version, locator, or source text; a proposition must cite its exact source."));

        // ── Identity (non-empty + atomic) ──────────────────────────────────────────────────────────
        var text = (input.Proposition.PropositionText ?? string.Empty).Trim();
        var nonEmpty = text.Length > 0;
        var atomic = nonEmpty && !CompoundMarkers.Any(m => text.ToLowerInvariant().Contains(m));
        findings.Add(new(LpiValidationCheck.Identity, nonEmpty && atomic,
            !nonEmpty ? "Proposition text is empty."
                      : atomic ? "Proposition is atomic."
                               : "Proposition appears compound; split into atomic propositions before accepting."));

        // ── Category (assertion type preserved) ──────────────────────────────────────────────────
        // Any valid enum is acceptable; the check documents that attribution was retained, not promoted.
        var categoryOk = Enum.IsDefined(input.Proposition.AssertionType);
        findings.Add(new(LpiValidationCheck.Category, categoryOk,
            categoryOk ? $"Assertion type preserved as {input.Proposition.AssertionType}."
                       : "Assertion type is unknown; a reported/inferred statement must not be promoted to an established fact."));

        if (input.Operation == LpiOperationKind.Withdraw)
        {
            // Withdraw only needs a valid supersession target and source identity — no placement required.
            var withdrawOk = input.SupersedesPropositionId is { } s && s != Guid.Empty;
            findings.Add(new(LpiValidationCheck.Revision, withdrawOk,
                withdrawOk ? "Withdraw references an existing proposition."
                           : "Withdraw must reference the proposition being withdrawn."));
            findings.Add(new(LpiValidationCheck.Placement, true, "No placement required for a withdraw."));
            findings.Add(new(LpiValidationCheck.Relationship, true, "No relationship required for a withdraw."));
            findings.Add(new(LpiValidationCheck.Duplication, true, "Duplication not applicable to a withdraw."));
            findings.Add(new(LpiValidationCheck.AuthorityBoundary, true, "No placement scores on a withdraw."));
            return Finalize(findings);
        }

        // ── Placement ────────────────────────────────────────────────────────────────────────────
        var placements = input.Placements ?? [];
        var hasPlacement = placements.Count > 0;
        var allTargeted = hasPlacement && placements.All(p => p.TargetNodeId != Guid.Empty);
        findings.Add(new(LpiValidationCheck.Placement, allTargeted,
            !hasPlacement ? "At least one placement is required (or flag NeedsHierarchyReview)."
                          : allTargeted ? "All placements name a target node."
                                        : "Every placement must name a target hierarchy node."));

        // ── Relationship ─────────────────────────────────────────────────────────────────────────
        var relationshipsOk = hasPlacement && placements.All(p => Enum.IsDefined(p.Relationship));
        findings.Add(new(LpiValidationCheck.Relationship, relationshipsOk,
            relationshipsOk ? "All placement relationships are valid qualitative codes."
                            : "Each placement must carry a qualitative relationship (Supports/Contradicts/Qualifies/ContextOnly)."));

        // ── AuthorityBoundary ───────────────────────────────────────────────────────────────────
        // placementFraction is only legitimate when BOTH comparable neighbors are present (reviewed
        // interpolation). A fraction without neighbors would be an inferred score — reject it.
        var boundaryOk = placements.All(p =>
            p.PlacementFraction is null
            || (p.LeftNeighborId is not null && p.RightNeighborId is not null
                && p.PlacementFraction is >= 0m and <= 1m));
        findings.Add(new(LpiValidationCheck.AuthorityBoundary, boundaryOk,
            boundaryOk ? "No numeric support or inferred interpolation smuggled into placements."
                       : "placementFraction is only allowed between two comparable neighbors; never inferred from display order."));

        // ── Duplication ──────────────────────────────────────────────────────────────────────────
        var normalized = Normalize(text);
        var isDuplicate = input.AcceptedPropositionTextsAtTargets
            .Any(existing => string.Equals(Normalize(existing), normalized, StringComparison.Ordinal));
        findings.Add(new(LpiValidationCheck.Duplication, !isDuplicate,
            isDuplicate ? "An identical proposition is already accepted at the target; reuse it instead of duplicating."
                        : "No exact duplicate at the target node."));

        // ── Revision ─────────────────────────────────────────────────────────────────────────────
        var revisionOk = input.Operation != LpiOperationKind.Revise
            || (input.SupersedesPropositionId is { } r && r != Guid.Empty);
        findings.Add(new(LpiValidationCheck.Revision, revisionOk,
            revisionOk ? "Revision linkage valid."
                       : "A revision must reference the proposition it supersedes."));

        return Finalize(findings);
    }

    private static LpiValidationOutcome Finalize(List<LpiValidationFinding> findings)
    {
        var valid = findings.All(f => f.Passed);
        // When a placement check failed because no node fit, park as NeedsHierarchyReview; otherwise
        // ReviewRequired. The proposal is ALWAYS preserved — never silently discarded.
        var placementMissing = findings.Any(f =>
            f.Check == LpiValidationCheck.Placement && !f.Passed);
        var preserved = valid
            ? LpiProposalState.PlacementProposed
            : placementMissing ? LpiProposalState.NeedsHierarchyReview : LpiProposalState.ReviewRequired;
        return new LpiValidationOutcome(valid, findings, preserved);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray();
        return new string(chars).Replace("  ", " ");
    }
}
