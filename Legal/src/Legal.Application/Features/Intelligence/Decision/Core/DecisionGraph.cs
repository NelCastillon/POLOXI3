using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2 — Dependency-Aware Decision graph Core (deterministic; authoritative).
//
// This is the Core layer for the typed legal dependency graph. The LLM proposes nodes/edges
// (DECISION_GRAPH) and per-edge verification opinions (DECISION_VERIFY); everything here is
// deterministic and owned by POLOXI:
//   • PropagateInvalidation — when an edge is INVALIDATED, weaken downstream nodes/edges along
//     typed relations without regenerating the answer (local failure propagation).
//   • RecomputeSupport      — materiality-weighted node support from verified incoming edges.
//   • EvaluateReadiness      — the dependency-constrained DECISION_READY predicate (hard gate).
//
// The graph is a mutable in-memory working set built from persistence rows; the caller persists the
// mutated state afterward. Node/edge vocabularies come from the shared constants in the contracts.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public static class DecisionGraph
{
    // A mutable working node. Kind is one of DecisionGraphNodeKinds.
    public sealed class Node
    {
        public required Guid Id { get; init; }
        public required string Kind { get; init; }
        public required string Code { get; init; }
        public string DisplayName { get; set; } = "";
        public string? Statement { get; set; }
        public double Support { get; set; }
        public bool IsEssential { get; set; }
        public bool IsSatisfied { get; set; }
        public string VerificationStatus { get; set; } = DecisionVerificationStates.Unverified;
        public int SortOrder { get; set; }
    }

    // A mutable working edge. Relation is one of DecisionGraphRelations.
    public sealed class Edge
    {
        public required Guid Id { get; init; }
        public required string Relation { get; init; }
        public required string SourceKind { get; init; }
        public required Guid SourceId { get; init; }
        public required string TargetKind { get; init; }
        public required Guid TargetId { get; init; }
        public double SupportWeight { get; set; }
        public double Materiality { get; set; }
        public bool IsEssential { get; set; }
        public bool IsDispositive { get; set; }
        public string VerificationStatus { get; set; } = DecisionVerificationStates.Unverified;
        public string? PropagatedStateCode { get; set; }
    }

    // The working graph: nodes keyed by id, plus adjacency for downstream traversal.
    public sealed class Model
    {
        public Dictionary<Guid, Node> Nodes { get; } = [];
        public List<Edge> Edges { get; } = [];

        public IEnumerable<Edge> OutgoingFrom(Guid nodeId) => Edges.Where(e => e.SourceId == nodeId);
        public IEnumerable<Edge> IncomingTo(Guid nodeId) => Edges.Where(e => e.TargetId == nodeId);
    }

    // Local failure propagation (§ propagation): starting from every INVALIDATED edge, weaken the
    // target node and cascade downstream along typed relations. Deterministic and depth-capped so it
    // can never oscillate. Returns the set of node ids whose support changed.
    public static IReadOnlyCollection<Guid> PropagateInvalidation(Model model, int maxDepth)
    {
        var changed = new HashSet<Guid>();
        // Seed frontier = targets of invalidated edges.
        var frontier = new Queue<(Guid NodeId, int Depth)>();
        foreach (var e in model.Edges.Where(e => e.VerificationStatus == DecisionVerificationStates.Invalidated))
        {
            e.PropagatedStateCode = "INVALIDATED_SOURCE";
            if (model.Nodes.ContainsKey(e.TargetId))
                frontier.Enqueue((e.TargetId, 0));
        }

        while (frontier.Count > 0)
        {
            var (nodeId, depth) = frontier.Dequeue();
            if (depth > maxDepth || !model.Nodes.TryGetValue(nodeId, out var node))
                continue;

            var before = node.Support;
            RecomputeSupport(model, node);
            var essentialBroken = model.IncomingTo(nodeId)
                .Any(e => e.IsEssential && e.VerificationStatus == DecisionVerificationStates.Invalidated);
            if (essentialBroken)
            {
                // An essential dependency failing collapses satisfaction and caps support at the ceiling.
                node.IsSatisfied = false;
                node.Support = Math.Min(node.Support, CeilingFromEssential(model, nodeId));
            }

            if (Math.Abs(node.Support - before) > 1e-6 || essentialBroken)
            {
                changed.Add(nodeId);
                // Cascade: mark downstream edges weakened and enqueue their targets.
                foreach (var outEdge in model.OutgoingFrom(nodeId))
                {
                    outEdge.PropagatedStateCode = "WEAKENED";
                    frontier.Enqueue((outEdge.TargetId, depth + 1));
                }
            }
        }

        return changed;
    }

    // Materiality-weighted node support (§ recompute): a node's support is the materiality-weighted
    // average of its non-invalidated incoming edges' (weight × source support). Invalidated edges
    // contribute nothing. Nodes with no incoming edges keep their proposed support.
    public static void RecomputeSupport(Model model, Node node)
    {
        var incoming = model.IncomingTo(node.Id)
            .Where(e => e.VerificationStatus != DecisionVerificationStates.Invalidated
                        && e.Relation != DecisionGraphRelations.Contradicts)
            .ToArray();
        if (incoming.Length == 0)
            return;

        double weightSum = 0, acc = 0;
        foreach (var e in incoming)
        {
            var srcSupport = model.Nodes.TryGetValue(e.SourceId, out var src) ? src.Support : e.SupportWeight;
            var m = Math.Max(e.Materiality, 1e-3);
            acc += m * Clamp01(e.SupportWeight * srcSupport);
            weightSum += m;
        }

        var contradictions = model.IncomingTo(node.Id)
            .Where(e => e.Relation == DecisionGraphRelations.Contradicts
                        && e.VerificationStatus != DecisionVerificationStates.Invalidated)
            .Sum(e => e.Materiality * e.SupportWeight);

        var support = weightSum <= 1e-9 ? node.Support : acc / weightSum;
        node.Support = Clamp01(support - contradictions);
        node.IsSatisfied = node.Support >= 0.5;
    }

    // The certainty ceiling for a node is the weakest of its essential incoming source supports (§16,§17).
    private static double CeilingFromEssential(Model model, Guid nodeId)
    {
        var essential = model.IncomingTo(nodeId).Where(e => e.IsEssential).ToArray();
        if (essential.Length == 0)
            return 1d;
        var min = 1d;
        foreach (var e in essential)
        {
            var s = e.VerificationStatus == DecisionVerificationStates.Invalidated
                ? 0d
                : (model.Nodes.TryGetValue(e.SourceId, out var src) ? src.Support : e.SupportWeight);
            min = Math.Min(min, Clamp01(s));
        }
        return min;
    }

    // The dependency-constrained DECISION_READY predicate (hard gate). Margin/entropy are NOT inputs
    // here — readiness is structural. Returns the verdict plus a per-clause checklist and blockers.
    public static DecisionReadinessVerdictDto EvaluateReadiness(
        Model model,
        bool winnerExists,
        double authorityVerifiedFractionRequired,
        int maxHighImpactFrontier,
        int openHighImpactFrontierCount,
        DecisionLosingSideTestDto? losingSide,
        double losingSideMargin)
    {
        var items = new List<DecisionReadinessItemDto>();
        var blockers = new List<string>();

        void Clause(string label, bool ok, string blocker, string? detail = null)
        {
            items.Add(new DecisionReadinessItemDto(label, ok, detail));
            if (!ok) blockers.Add(blocker);
        }

        // 1. Winner exists.
        Clause("Winner exists", winnerExists, "No winning candidate.");

        // 2. Essential dependencies satisfied.
        var essentialNodes = model.Nodes.Values.Where(n => n.IsEssential).ToArray();
        var essentialOk = essentialNodes.All(n => n.IsSatisfied);
        Clause("Essential dependencies satisfied", essentialNodes.Length == 0 || essentialOk,
            "One or more essential dependencies are unsatisfied.",
            $"{essentialNodes.Count(n => n.IsSatisfied)}/{essentialNodes.Length} essential nodes satisfied.");

        // 3. Material authority verified.
        var materialAuthority = model.Edges
            .Where(e => e.SourceKind == DecisionGraphNodeKinds.Evidence || e.TargetKind == DecisionGraphNodeKinds.Proposition)
            .Where(e => e.Materiality >= 0.5)
            .ToArray();
        var verifiedFraction = materialAuthority.Length == 0
            ? 1d
            : (double)materialAuthority.Count(e => e.VerificationStatus == DecisionVerificationStates.Verified) / materialAuthority.Length;
        Clause("Material authority verified", verifiedFraction >= authorityVerifiedFractionRequired,
            "Material authority is not sufficiently verified.",
            $"{verifiedFraction:P0} of material authority verified.");

        // 4. Burden satisfied.
        var burdens = model.Nodes.Values.Where(n => n.Kind == DecisionGraphNodeKinds.Burden).ToArray();
        var burdenOk = burdens.All(n => n.IsSatisfied);
        Clause("Burden satisfied", burdens.Length == 0 || burdenOk, "An applicable burden of proof is not satisfied.");

        // 5. Procedure satisfied (dispositive procedural constraints must hold).
        var procedures = model.Nodes.Values.Where(n => n.Kind == DecisionGraphNodeKinds.Procedure).ToArray();
        var procedureOk = procedures.All(n => n.IsSatisfied);
        Clause("Procedure satisfied", procedures.Length == 0 || procedureOk, "A procedural constraint is not satisfied.");

        // 6. Losing-side test passed.
        var losingOk = losingSide is not null && losingSide.WinnerSurvived
            && (double)(losingSide.WinnerStrength - losingSide.ChallengerStrength) >= losingSideMargin;
        Clause("Strongest-loser test passed", losingOk,
            "The winner did not survive the strongest permissible opposing case by the required margin.",
            losingSide is null ? "Losing-side test not run." : $"Margin {losingSide.WinnerStrength - losingSide.ChallengerStrength:0.00}.");

        // 7. No high-impact open frontier.
        Clause("No high-impact open frontier", openHighImpactFrontierCount <= maxHighImpactFrontier,
            $"{openHighImpactFrontierCount} high-impact frontier item(s) still open.");

        // 8. No dispositive verifier failure.
        var dispositiveFailure = model.Edges.Any(e => e.IsDispositive && e.VerificationStatus == DecisionVerificationStates.Invalidated);
        Clause("No dispositive verifier failure", !dispositiveFailure, "A dispositive edge was invalidated by the verifier.");

        return new DecisionReadinessVerdictDto(blockers.Count == 0, blockers, items);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
