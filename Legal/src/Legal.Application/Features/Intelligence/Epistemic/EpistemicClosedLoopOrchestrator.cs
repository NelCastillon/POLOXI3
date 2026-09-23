using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-4 closed-loop orchestrator (§18, §27, §33, §34).
//
// Turns a verified claim into a NEW POLOXI investigation by composing the pieces already built:
//
//   claim verification (EA-3)  →  IF a NEW verification event was recorded AND the claim binds to a
//   graph edge  →  deterministic dependency propagation (V2.1)  →  outcome-directed ResearchNeed.
//
// Invariants preserved:
//   • Propagation runs at most once per distinct verification transition (idempotency from EA-3).
//   • The graph is the sole propagator; EA-4 never rescatters authoritative POLOXI state itself.
//   • A claim with no verified edge binding still verifies + persists, but drives no propagation
//     (it cannot silently move the ranking without a real dependency link).
//   • Claim→edge binding is explicit or resolved from lineage (SourceBranchId/SourceCandidateId);
//     it is never guessed from claim text.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record ClaimLoopRequest
{
    public required ClaimVerificationRequest Verification { get; init; }

    // The current decision-graph snapshot the propagation runs against.
    public required DecisionGraphPersistence Graph { get; init; }

    // Branch snapshot used to select the next-highest-IV research need after propagation.
    public IReadOnlyList<DecisionBranchPersistence> Branches { get; init; } = [];

    public required Guid SessionId { get; init; }

    public Guid? MatterId { get; init; }

    // Bounds propagation traversal depth (defaults to the caller-provided V2.1 setting).
    public int MaxPropagationDepth { get; init; } = 3;
}

public sealed record ClaimLoopResult
{
    public required ClaimVerificationOutcome Verification { get; init; }

    // True only when a NEW verification event was recorded (so propagation was eligible to run).
    public bool VerificationEventRecorded { get; init; }

    // The graph edge the claim was bound to, if any.
    public Guid? BoundEdgeId { get; init; }

    // True when propagation actually ran (event recorded AND an edge binding was found).
    public bool PropagationRan { get; init; }

    public DependencyImpact Impact { get; init; } = DependencyImpact.Empty;

    // The mutated graph model after propagation (caller persists it); null when propagation did not run.
    public DecisionGraph.Model? UpdatedModel { get; init; }

    // The outcome-directed research need produced from the post-propagation frontier, if any.
    public DecisionResearchNeedPersistence? ResearchNeed { get; init; }

    public IReadOnlyList<string> AuditNarrative { get; init; } = [];
}

public interface IEpistemicClosedLoopOrchestrator
{
    Task<ClaimLoopResult> RunAsync(ClaimLoopRequest request, CancellationToken cancellationToken = default);
}

public sealed class EpistemicClosedLoopOrchestrator(
    IMaterialClaimVerificationService verificationService,
    IDependencyPropagationService propagationService,
    EpistemicAuthoritySettings settings)
    : IEpistemicClosedLoopOrchestrator
{
    public async Task<ClaimLoopResult> RunAsync(ClaimLoopRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Verification);
        ArgumentNullException.ThrowIfNull(request.Graph);

        var narrative = new List<string>();

        // 1. Verify the claim (EA-3). Always persists; only a NEW event enables propagation.
        var outcome = await verificationService.VerifyAsync(request.Verification, cancellationToken);
        narrative.Add(
            $"Claim {outcome.Claim.ClaimId:N} verified: {ClaimCodes.ToCode(outcome.PreviousState)} -> " +
            $"{ClaimCodes.ToCode(outcome.NewState)} (authority={outcome.Authority.DecisionAuthority}).");

        if (!outcome.VerificationEventRecorded)
        {
            narrative.Add("No new verification event (idempotent replay); propagation skipped.");
            return new ClaimLoopResult
            {
                Verification = outcome,
                VerificationEventRecorded = false,
                AuditNarrative = narrative,
            };
        }

        // 2. Resolve the claim → graph-edge binding (explicit first, then lineage). No binding means
        //    the claim cannot move the ranking — it verified and persisted, but drives no propagation.
        var edgeId = ResolveEdgeId(request.Verification, outcome.Claim, request.Graph);
        if (edgeId is null)
        {
            narrative.Add("Claim has no bound dependency edge; verification persisted without propagation.");
            return new ClaimLoopResult
            {
                Verification = outcome,
                VerificationEventRecorded = true,
                AuditNarrative = narrative,
            };
        }

        // §34 feature flag: EA-4 graph propagation can be disabled in isolation. When off, the claim
        // still verifies and persists, but no graph-derived signals feed POLOXI Core.
        if (!settings.UseClaimDependencyPropagation)
        {
            narrative.Add("Claim dependency propagation disabled (UseClaimDependencyPropagation=false); propagation skipped.");
            return new ClaimLoopResult
            {
                Verification = outcome,
                VerificationEventRecorded = true,
                BoundEdgeId = edgeId,
                AuditNarrative = narrative,
            };
        }

        // 3. Deterministic dependency propagation on the mapped edge status (V2.1 owns the mutation).
        var propagation = propagationService.Apply(
            request.Graph, edgeId.Value, outcome.MappedEdgeStatus, request.MaxPropagationDepth);

        if (!propagation.EdgeFound)
        {
            narrative.Add($"Bound edge {edgeId:N} not present in graph snapshot; propagation skipped.");
            return new ClaimLoopResult
            {
                Verification = outcome,
                VerificationEventRecorded = true,
                BoundEdgeId = edgeId,
                AuditNarrative = narrative,
            };
        }

        narrative.Add(
            $"Propagated edge {edgeId:N} {propagation.PreviousStatus ?? "?"} -> {outcome.MappedEdgeStatus}: " +
            $"{propagation.Impact.AffectedBranchIds.Count} branch(es), " +
            $"{propagation.Impact.AffectedCandidateIds.Count} candidate(s) affected" +
            (propagation.Impact.RecompetitionRequired ? "; recompetition requested." : "."));

        // 4. Outcome-directed research need from the post-propagation frontier (§18).
        var researchNeed = DecisionResearchNeedFactory.Create(
            request.Branches,
            propagation.Impact,
            request.SessionId,
            request.Verification.TenantId,
            request.Verification.ActorUserId,
            request.MatterId,
            dependencyEventId: null);

        if (researchNeed is not null)
            narrative.Add($"Next research need: '{researchNeed.IssueLabel}' (IV={researchNeed.InformationValue:0.##}).");
        else
            narrative.Add("No open frontier remains; nothing further worth researching from this change.");

        return new ClaimLoopResult
        {
            Verification = outcome,
            VerificationEventRecorded = true,
            BoundEdgeId = edgeId,
            PropagationRan = true,
            Impact = propagation.Impact,
            UpdatedModel = propagation.Model,
            ResearchNeed = researchNeed,
            AuditNarrative = narrative,
        };
    }

    // Explicit binding wins; otherwise match the claim's lineage (branch, then candidate) to a graph
    // edge's source. Returns null when no deterministic binding exists — never a fuzzy/text guess.
    private static Guid? ResolveEdgeId(
        ClaimVerificationRequest request,
        ClaimProposition claim,
        DecisionGraphPersistence graph)
    {
        if (request.DependencyEdgeId is { } explicitEdge)
            return explicitEdge;

        if (claim.SourceBranchId is { } branchId)
        {
            var byBranch = graph.Edges.FirstOrDefault(e => e.SourceBranchId == branchId);
            if (byBranch is not null)
                return byBranch.EdgeId;
        }

        if (claim.SourceCandidateId is { } candidateId)
        {
            var byCandidate = graph.Edges.FirstOrDefault(e => e.SourceCandidateId == candidateId);
            if (byCandidate is not null)
                return byCandidate.EdgeId;
        }

        return null;
    }
}
