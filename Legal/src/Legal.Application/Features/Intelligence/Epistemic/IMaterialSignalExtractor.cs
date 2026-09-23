using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — material-signal extraction (frozen slice-2 design).
//
// Extraction is defined behind an interface so a richer (LLM-backed) extractor can replace the
// deterministic default without touching the orchestrator. This slice ships the deterministic
// GraphMaterialSignalExtractor: it derives DecisionSupportSignals directly from the already-built
// V2 dependency graph (assertion nodes + their edges) — no new model call, fully reproducible.
//
// Mapping rules (assertion nodes only; STRATEGY/CANDIDATE/BRANCH nodes are lineage anchors):
//   • VerificationState: an incoming VERIFIED edge ⇒ Supported; an INVALIDATED (or CONTRADICTS)
//     edge ⇒ Contradicted; otherwise Unverified. Contradiction wins over verification.
//   • RequiresVerification: essential nodes always require it; non-essential nodes require it when
//     they carry material support (materiality > 0).
//   • DecisionImpact: the node's Support weight.
//   • Lineage: the branch/candidate the node depends on (resolved from outgoing dependency edges,
//     falling back to incoming edges). No lineage ⇒ the projection service drops the signal.
// ─────────────────────────────────────────────────────────────────────────────────────────────

// Context for a single extraction pass over a decision session's V2 graph.
public sealed record MaterialSignalExtractionContext
{
    public required Guid SessionId { get; init; }
    public required Guid TenantId { get; init; }
    public Guid? MatterId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ProposedByModel { get; init; }
    public string? PromptRunId { get; init; }
    public required IReadOnlyList<DecisionGraphNodeDto> Nodes { get; init; }
    public required IReadOnlyList<DecisionGraphEdgeDto> Edges { get; init; }
}

public interface IMaterialSignalExtractor
{
    // Produces the verifiable decision-support signals implied by the graph. Deterministic and pure:
    // the same graph always yields the same signal set. Never mutates the graph or POLOXI state.
    IReadOnlyList<DecisionSupportSignal> Extract(MaterialSignalExtractionContext context);
}

public sealed class GraphMaterialSignalExtractor : IMaterialSignalExtractor
{
    // Node kinds that represent verifiable assertions (as opposed to lineage anchors).
    private static readonly HashSet<string> AssertionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        DecisionGraphNodeKinds.Fact,
        DecisionGraphNodeKinds.Proposition,
        DecisionGraphNodeKinds.Element,
        DecisionGraphNodeKinds.Burden,
        DecisionGraphNodeKinds.Evidence,
    };

    public IReadOnlyList<DecisionSupportSignal> Extract(MaterialSignalExtractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nodesById = context.Nodes.ToDictionary(n => n.NodeId);
        var signals = new List<DecisionSupportSignal>();

        foreach (var node in context.Nodes)
        {
            if (!AssertionKinds.Contains(node.NodeKind))
                continue;

            var (branchId, candidateId) = ResolveLineage(node, context, nodesById);

            // No lineage → the signal cannot move the ranking; skip it deterministically.
            if (branchId is null && candidateId is null)
                continue;

            var state = ResolveVerificationState(node, context);
            var requiresVerification = node.IsEssential || node.Support > 0m;

            var statement = string.IsNullOrWhiteSpace(node.Statement) ? node.DisplayName : node.Statement!;

            signals.Add(new DecisionSupportSignal
            {
                DecisionSessionId = context.SessionId,
                MatterId = context.MatterId,
                Statement = statement,
                NormalizedStatement = Normalize(statement),
                Origin = DecisionSupportOrigin.LlmGenerated,
                VerificationState = state,
                RequiresVerification = requiresVerification,
                VerificationStrength = state == DecisionSupportVerificationState.Supported ? Clamp01(node.Support) : 0m,
                DecisionImpact = Clamp01(node.Support),
                SourceBranchId = branchId,
                SourceCandidateId = candidateId,
                ProposedByModel = context.ProposedByModel,
                PromptRunId = context.PromptRunId,
            });
        }

        return signals;
    }

    // VERIFIED incoming edge ⇒ Supported; INVALIDATED/CONTRADICTS ⇒ Contradicted (wins); else Unverified.
    private static DecisionSupportVerificationState ResolveVerificationState(
        DecisionGraphNodeDto node,
        MaterialSignalExtractionContext context)
    {
        var incoming = context.Edges.Where(e => e.TargetNodeId == node.NodeId).ToArray();

        var contradicted = incoming.Any(e =>
            string.Equals(e.VerificationStatus, DecisionVerificationStates.Invalidated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.RelationCode, DecisionGraphRelations.Contradicts, StringComparison.OrdinalIgnoreCase));
        if (contradicted)
            return DecisionSupportVerificationState.Contradicted;

        var verified = incoming.Any(e =>
            string.Equals(e.VerificationStatus, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase));
        return verified ? DecisionSupportVerificationState.Supported : DecisionSupportVerificationState.Unverified;
    }

    // Resolve the branch/candidate lineage the node depends on. Prefer an outgoing dependency edge to
    // a CANDIDATE/BRANCH anchor; fall back to an incoming edge from such an anchor.
    private static (Guid? BranchId, Guid? CandidateId) ResolveLineage(
        DecisionGraphNodeDto node,
        MaterialSignalExtractionContext context,
        IReadOnlyDictionary<Guid, DecisionGraphNodeDto> nodesById)
    {
        Guid? branchId = null;
        Guid? candidateId = null;

        foreach (var edge in context.Edges)
        {
            Guid? anchorId = edge.SourceNodeId == node.NodeId ? edge.TargetNodeId
                : edge.TargetNodeId == node.NodeId ? edge.SourceNodeId
                : null;
            if (anchorId is null || !nodesById.TryGetValue(anchorId.Value, out var anchor))
                continue;

            if (branchId is null && string.Equals(anchor.NodeKind, DecisionGraphNodeKinds.Branch, StringComparison.OrdinalIgnoreCase))
                branchId = anchor.NodeId;
            else if (candidateId is null && string.Equals(anchor.NodeKind, DecisionGraphNodeKinds.Candidate, StringComparison.OrdinalIgnoreCase))
                candidateId = anchor.NodeId;

            if (branchId is not null && candidateId is not null)
                break;
        }

        return (branchId, candidateId);
    }

    private static decimal Clamp01(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;

    private static string Normalize(string text)
        => string.Join(' ', text.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
