using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision;

// ── Attorney Decision Input (ADI) orchestration service (§19) ────────────────────────────────────
// Coordinates the Add-Proposition workflow (Define → Placement → Preview → Confirm) plus node-scoped
// assessment/approval/challenge/reposition. Deterministic structural checks and midpoint suggestion
// live here; the authoritative score projection is delegated to the thin POLOXI adapter. This service
// never defines a new final formula, never averages attorneys, and never rewrites history.
public sealed class AttorneyDecisionInputService(
    IAttorneyDecisionInputRepository repository,
    IExistingPoloxiEvaluationAdapter poloxiAdapter,
    ILogger<AttorneyDecisionInputService> logger) : IAttorneyDecisionInputService
{
    private const decimal MinValue = 0m;
    private const decimal MaxValue = 100m;

    public async Task<AttorneyInputDraft> CreateDraftAsync(Guid tenantId, Guid actorUserId, CreateAttorneyInputCommand command, CancellationToken cancellationToken = default)
    {
        var text = (command.NodeText ?? string.Empty).Trim();
        if (text.Length == 0)
            throw new ArgumentException("Proposition text is required.", nameof(command));

        // Deterministic APR atomicity heuristic for L3+ (§8). L1/L2 are not forcibly atomized.
        var atomicity = command.NodeLevel >= 3 ? EvaluateAtomicity(text) : "NotApplicable";

        // Duplicate shortlist (§8) — canonical reuse candidates within the same matter.
        var duplicates = await repository.FindDuplicateCandidatesAsync(tenantId, command.MatterId, text, cancellationToken);

        // Parent-fidelity check is advisory here; a full AI fit check may run when the model route exists.
        var (fidelity, suggestedParent) = ("Good", (string?)null);

        return new AttorneyInputDraft(
            DraftId: Guid.NewGuid(),
            command.MatterId, command.ParentNodeId, command.CandidateNodeId, command.NodeKindCode, command.NodeLevel,
            text, command.Rationale, atomicity, fidelity, suggestedParent, duplicates,
            LegalContextSummary: BuildLegalContextSummary(command.NodeKindCode, command.NodeLevel),
            AiChecksAvailable: false);
    }

    public async Task<PlacementAnalysis> AnalyzePlacementAsync(Guid tenantId, AnalyzePlacementCommand command, CancellationToken cancellationToken = default)
    {
        var siblings = await repository.GetSiblingValuesAsync(
            tenantId, command.MatterId, command.CandidateNodeId, command.ParentNodeId, command.NodeLevel, cancellationToken);

        decimal? prevValue = null, nextValue = null;
        string? prevText = null, nextText = null;
        Guid? prevId = command.PreviousSiblingId, nextId = command.NextSiblingId;

        foreach (var s in siblings)
        {
            if (prevId is { } p && s.NodeId == p) { prevValue = s.Value; prevText = s.NodeText; }
            if (nextId is { } n && s.NodeId == n) { nextValue = s.Value; nextText = s.NodeText; }
        }

        // §4: only interpolate when both comparable neighbors exist within the same parent/level/scope.
        decimal? midpoint = null;
        var comparable = prevValue is not null && nextValue is not null;
        string method;
        decimal lower = MinValue, upper = MaxValue;
        if (comparable)
        {
            midpoint = Math.Round((prevValue!.Value + nextValue!.Value) / 2m, 4);
            lower = Math.Min(prevValue.Value, nextValue.Value);
            upper = Math.Max(prevValue.Value, nextValue.Value);
            method = "Midpoint";
        }
        else if (prevValue is not null || nextValue is not null)
        {
            // Only one comparable sibling — never invent an arbitrary ± rule (§4). Bounded explicit entry.
            method = "BoundedEntry";
        }
        else
        {
            method = "PendingAssessment";
        }

        const string disclosure =
            "This position has scoring meaning. The midpoint is a professional-judgment suggestion, not an evidence score. "
            + "Confirm it or deliberately adjust it within the neighboring range.";

        return new PlacementAnalysis(
            prevId, prevText, prevValue, nextId, nextText, nextValue, midpoint, lower, upper, method, comparable,
            comparable ? disclosure : disclosure + " No two comparable siblings exist yet; enter a bounded value.");
    }

    public Task<DecisionMutationPreview> PreviewAsync(Guid tenantId, Guid actorUserId, PreviewAttorneyInputCommand command, CancellationToken cancellationToken = default)
        // §15: reuse the existing POLOXI evaluation entry point as a thin, non-committing adapter.
        => poloxiAdapter.ProjectAsync(tenantId, actorUserId, command, cancellationToken);

    public async Task<CommitResult> CommitAsync(Guid tenantId, Guid actorUserId, CommitAttorneyInputCommand command, CancellationToken cancellationToken = default)
    {
        // Idempotency (§23): a replayed commit returns the original result without a second mutation.
        var existing = await repository.TryGetCommittedAsync(tenantId, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Idempotent ADI commit replay for key {Key}; returning original result.", command.IdempotencyKey);
            return existing;
        }

        // Stale-state guard (§16/§23): reject if the hierarchy moved since the preview was prepared.
        var currentVersion = await repository.GetHierarchyVersionAsync(tenantId, command.MatterId, cancellationToken);
        if (command.ExpectedHierarchyVersion != 0 && currentVersion != command.ExpectedHierarchyVersion)
            throw new InvalidOperationException("The decision model changed since preview. Review updated neighbors and impact before submitting.");

        var value = Math.Clamp(command.ConfirmedValue, MinValue, MaxValue);
        var canonicalKey = BuildCanonicalKey(command.CandidateNodeId, command.NodeLevel, command.NodeText);
        var placementKey = BuildPlacementKey(command.PreviousSiblingValue, command.NextSiblingValue, value);

        var normalized = command with { ConfirmedValue = value };
        return await repository.CommitAttorneyInputAsync(tenantId, actorUserId, normalized, canonicalKey, placementKey, cancellationToken);
    }

    public Task<AttorneyRelativeAssessmentDto> SubmitAssessmentAsync(Guid tenantId, Guid actorUserId, SubmitAttorneyAssessmentCommand command, CancellationToken cancellationToken = default)
    {
        var value = Math.Clamp(command.ConfirmedValue, MinValue, MaxValue);
        return repository.SubmitAssessmentAsync(tenantId, actorUserId, command with { ConfirmedValue = value }, cancellationToken);
    }

    public Task<ApprovedMatterAssessmentDto> ApproveAssessmentAsync(Guid tenantId, Guid actorUserId, ApproveMatterAssessmentCommand command, CancellationToken cancellationToken = default)
        => repository.ApproveAssessmentAsync(tenantId, actorUserId, command, cancellationToken);

    public Task<Guid> RaiseChallengeAsync(Guid tenantId, Guid actorUserId, RaiseChallengeCommand command, CancellationToken cancellationToken = default)
        => repository.RaiseChallengeAsync(tenantId, actorUserId, command, cancellationToken);

    public Task<CommitResult> RepositionAsync(Guid tenantId, Guid actorUserId, RepositionNodeCommand command, CancellationToken cancellationToken = default)
    {
        var value = Math.Clamp(command.ConfirmedValue, MinValue, MaxValue);
        return repository.RepositionAsync(tenantId, actorUserId, command with { ConfirmedValue = value }, cancellationToken);
    }

    // Deterministic atomicity heuristic — a conservative pre-check, not the authoritative APR gate (§8).
    private static string EvaluateAtomicity(string text)
    {
        var lower = text.ToLowerInvariant();
        // Compound coordination markers suggest the proposition should be decomposed.
        string[] compoundMarkers = [" and ", " and/or ", "; ", " as well as ", " in addition to ", " both "];
        if (compoundMarkers.Any(lower.Contains))
            return "Compound";
        // Vague/ambiguous quantifiers warrant clarification before acceptance.
        string[] ambiguousMarkers = ["etc", "and so on", "various", "several things", "among others"];
        if (ambiguousMarkers.Any(lower.Contains))
            return "Ambiguous";
        return "Atomic";
    }

    private static string BuildLegalContextSummary(string kind, int level) => kind switch
    {
        "Candidate" => "L1 competing outcome/candidate. Not forcibly atomized.",
        "Factor" => "L2 material decision factor. Not forcibly atomized.",
        _ => $"L{level} decision proposition. Evaluated for atomicity, parent fidelity and coverage."
    };

    // Stable canonical key that keeps attorney and LLM nodes in the same namespace (§6/§7).
    private static string BuildCanonicalKey(Guid candidateId, int level, string text)
    {
        var slug = new string(text.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
            .Replace(' ', '-');
        if (slug.Length > 80) slug = slug[..80];
        return $"adi:{candidateId:N}:l{level}:{slug}";
    }

    // Fractional/lexicographic placement key derived from neighbor values (§7). Falls back to the value.
    private static string BuildPlacementKey(decimal? prev, decimal? next, decimal value)
    {
        var basis = prev is not null && next is not null ? (prev.Value + next.Value) / 2m : value;
        // Zero-padded, monotonic key so ORDER BY PlacementKey preserves intended sibling order.
        return (basis * 10000m).ToString("00000000");
    }
}
