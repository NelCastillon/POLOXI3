using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision;

// ── Thin POLOXI preview adapter (§13, §15) ───────────────────────────────────────────────────────
// Projects a proposed (non-committing) attorney mutation onto the EXISTING POLOXI scoring surface so
// the Add-Proposition wizard can show candidate / uncertainty / IV / frontier / readiness deltas
// BEFORE commit. It never mutates authoritative state, never defines a new final formula, and never
// grants an origin bonus. The projected candidate score is explicitly advisory: the authoritative
// recompute is performed by POLOXI's async affected-closure worker after commit (§13, §16, §26).
public sealed class ExistingPoloxiEvaluationAdapter(
    IAttorneyDecisionInputRepository repository,
    IDecisionIntegrityRepository integrityRepository,
    ILogger<ExistingPoloxiEvaluationAdapter> logger) : IExistingPoloxiEvaluationAdapter
{
    public async Task<DecisionMutationPreview> ProjectAsync(
        Guid tenantId, Guid actorUserId, PreviewAttorneyInputCommand command, CancellationToken cancellationToken = default)
    {
        var findings = new List<string>();

        // Load the base snapshot for readiness/frontier context. A stale/missing snapshot is surfaced,
        // not fabricated (§26): preview remains advisory and requires re-preview if the snapshot moved.
        DecisionSnapshotDto? snapshot = null;
        try
        {
            snapshot = await integrityRepository.GetSnapshotAsync(tenantId, command.BaseSnapshotId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preview snapshot load failed for {SnapshotId}; projecting advisory only.", command.BaseSnapshotId);
        }
        if (snapshot is null)
            findings.Add("STALE_SNAPSHOT: base snapshot unavailable; projection is advisory and must be re-previewed before commit.");

        // Pull the current sibling values in the target scope so the projection reflects the real
        // neighborhood the attorney is inserting into (§4). No new formula — the neighborhood mean is
        // used only as an advisory candidate proxy until POLOXI's authoritative recompute runs.
        var siblings = await repository.GetSiblingValuesAsync(
            tenantId, command.MatterId, command.CandidateNodeId, command.ParentNodeId, command.NodeLevel, cancellationToken);
        var siblingValues = siblings.Where(s => s.Value is not null).Select(s => s.Value!.Value).ToArray();

        decimal currentCandidate = siblingValues.Length > 0 ? Math.Round(siblingValues.Average(), 2) : 0m;
        // Projection folds the confirmed attorney value into the neighborhood as one additional child.
        var projectedValues = siblingValues.Append(Math.Clamp(command.ConfirmedValue, 0m, 100m)).ToArray();
        decimal projectedCandidate = Math.Round(projectedValues.Average(), 2);
        var candidateDelta = Math.Round(projectedCandidate - currentCandidate, 2);

        // Advisory uncertainty proxy: inserting a node with an unresolved dependency can raise
        // uncertainty even before evidence changes (§14). Bounded, deterministic, display-only.
        var spread = siblingValues.Length > 1
            ? (siblingValues.Max() - siblingValues.Min()) / 100m
            : 0.05m;
        var uncertaintyDelta = Math.Round(Math.Clamp(spread * 0.2m, 0m, 0.10m), 4);

        // Advisory IV band (§14): higher when the neighborhood is sparse or the insertion is
        // high-impact relative to neighbors. Attorney origin alone cannot force HIGH (§14 invariant).
        var impact = Math.Abs(candidateDelta);
        var ivDelta = Math.Round(Math.Clamp(impact / 100m + uncertaintyDelta, 0m, 1m), 4);
        var ivBand = impact >= 2m || siblingValues.Length <= 1 ? "High" : impact >= 0.5m ? "Medium" : "Low";

        var frontierChanged = ivBand == "High";

        var affectedPath = BuildAffectedPath(command.NodeLevel);

        var readinessBefore = snapshot?.ReadinessStatusCode;
        var readinessAfter = frontierChanged ? "ReviewRecommended" : readinessBefore;

        var explanation =
            $"Attorney assessment {command.ConfirmedValue:0.#} folds into {siblingValues.Length} comparable sibling(s) "
            + $"(current ≈ {currentCandidate:0.#} → projected ≈ {projectedCandidate:0.#}, Δ {candidateDelta:+0.#;-0.#;0}). "
            + $"Uncertainty Δ +{uncertaintyDelta:0.####}; Information Value {ivBand}. "
            + "Projection is advisory; POLOXI performs the authoritative affected-closure recompute after commit.";

        return new DecisionMutationPreview(
            PreviewToken: Guid.NewGuid(),
            command.BaseSnapshotId,
            command.CandidateNodeId,
            CandidateText: siblings.FirstOrDefault().NodeText ?? "Candidate",
            currentCandidate,
            projectedCandidate,
            candidateDelta,
            uncertaintyDelta,
            ivDelta,
            ivBand,
            frontierChanged,
            readinessBefore,
            readinessAfter,
            affectedPath,
            findings,
            ScoreProjectionAdvisory: true,
            explanation);
    }

    // §13 affected-closure path shape: L5 → L4 → L3 → L2 → L1 candidate → competition.
    private static IReadOnlyCollection<string> BuildAffectedPath(int level)
    {
        var path = new List<string>();
        for (var l = level; l >= 1; l--)
            path.Add($"L{l}");
        path.Add("Candidate Competition");
        return path;
    }
}
