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

        // Kind-specific attributes carried through to the node's persistence table. For Burden nodes
        // BurdenedParty is mandatory at the DB boundary; the proposal validator guarantees it is set.
        public string? AuthorityRef { get; set; }
        public string? BurdenedParty { get; set; }
        public string? StandardOfProof { get; set; }

        // V2.1 lineage: which authoritative POLOXI object this node derives from (no string matching).
        public Guid? SourceBranchId { get; set; }
        public Guid? SourceCandidateId { get; set; }
        public Guid? SourceEvidenceId { get; set; }
        public string? SourceAuthorityId { get; set; }
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

        // V2.1: an edge may be bypassed if another VERIFIED path independently establishes the target.
        public bool AlternativePathAllowed { get; set; }
        public string PropagationPolicy { get; set; } = "DEPENDENCY";
        public Guid? SourceBranchId { get; set; }
        public Guid? SourceCandidateId { get; set; }
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

    // ── V2.1: impact-producing propagation (§6, §7). ──────────────────────────────────────────────
    // Deterministically evaluates the impact of a dependency-state change WITHOUT rescoring
    // candidates. Returns a structured DependencyImpact whose affected ids + domain-neutral signals
    // are handed to POLOXI Core for authoritative recompetition. Alternative verified paths are
    // honored: a target does not fail if another VERIFIED, non-invalidated path establishes it.
    public static DependencyImpact PropagateImpact(Model model, int maxDepth)
    {
        var changedNodes = new HashSet<Guid>();
        var changedEdges = new HashSet<Guid>();
        var affectedBranches = new HashSet<Guid>();
        var affectedCandidates = new HashSet<Guid>();
        var essentialFailed = new HashSet<Guid>();
        var essentialUnverified = new HashSet<Guid>();
        var signals = new List<DecisionBranchSignal>();

        // Snapshot support before mutation so we can compute a signed per-node delta.
        var supportBefore = model.Nodes.Values.ToDictionary(n => n.Id, n => n.Support);

        var frontier = new Queue<(Guid NodeId, int Depth)>();
        foreach (var e in model.Edges)
        {
            if (e.VerificationStatus == DecisionVerificationStates.Invalidated)
            {
                changedEdges.Add(e.Id);
                e.PropagatedStateCode = "INVALIDATED_SOURCE";
                if (model.Nodes.ContainsKey(e.TargetId))
                    frontier.Enqueue((e.TargetId, 0));
            }
        }

        var visited = new HashSet<Guid>();
        while (frontier.Count > 0)
        {
            var (nodeId, depth) = frontier.Dequeue();
            if (depth > maxDepth || !model.Nodes.TryGetValue(nodeId, out var node))
                continue;
            // Cycle/bounded-work safety: process each node once per propagation pass.
            if (!visited.Add(nodeId))
                continue;

            var before = node.Support;
            RecomputeSupport(model, node);

            // An essential incoming dependency is broken only when NO alternative verified path
            // independently establishes this node (§6 alternative-path detection).
            var essentialInvalidated = model.IncomingTo(nodeId)
                .Where(e => e.IsEssential && e.VerificationStatus == DecisionVerificationStates.Invalidated)
                .ToArray();
            var hasAlternativeVerifiedPath = model.IncomingTo(nodeId).Any(e =>
                e.VerificationStatus == DecisionVerificationStates.Verified
                && e.Relation != DecisionGraphRelations.Contradicts
                && (model.Nodes.TryGetValue(e.SourceId, out var s) ? s.Support : e.SupportWeight) >= 0.5);

            var essentialBroken = essentialInvalidated.Length > 0
                && !essentialInvalidated.All(e => e.AlternativePathAllowed && hasAlternativeVerifiedPath);

            if (essentialBroken)
            {
                node.IsSatisfied = false;
                node.Support = Math.Min(node.Support, CeilingFromEssential(model, nodeId));
                essentialFailed.Add(nodeId);
            }

            // Track essential-but-unverified dependencies (blocks readiness without failing support).
            foreach (var e in model.IncomingTo(nodeId).Where(e => e.IsEssential && e.VerificationStatus == DecisionVerificationStates.Unverified))
                essentialUnverified.Add(e.Id);

            if (Math.Abs(node.Support - before) > 1e-6 || essentialBroken)
            {
                changedNodes.Add(nodeId);
                foreach (var outEdge in model.OutgoingFrom(nodeId))
                {
                    changedEdges.Add(outEdge.Id);
                    outEdge.PropagatedStateCode = "WEAKENED";
                    frontier.Enqueue((outEdge.TargetId, depth + 1));
                }
            }
        }

        // Fold changed nodes into affected branches/candidates + domain-neutral signals via lineage.
        foreach (var id in changedNodes)
        {
            if (!model.Nodes.TryGetValue(id, out var node))
                continue;
            var deltaRaw = node.Support - (supportBefore.TryGetValue(id, out var b) ? b : node.Support);
            var delta = Math.Clamp(deltaRaw, -1d, 1d);
            var branchId = ResolveLineageBranch(model, node);
            var candidateId = node.SourceCandidateId;
            if (branchId is { } bid) affectedBranches.Add(bid);
            if (candidateId is { } cid) affectedCandidates.Add(cid);

            var reopen = essentialFailed.Contains(id);
            var kind = reopen ? DecisionBranchSignalKinds.ReopenRequested
                : Math.Abs(delta) > 1e-6 ? DecisionBranchSignalKinds.SupportChanged
                : DecisionBranchSignalKinds.EvidenceChanged;
            signals.Add(new DecisionBranchSignal(
                kind, branchId, candidateId, delta, reopen,
                reopen ? "ESSENTIAL_DEPENDENCY_FAILED" : delta < 0 ? "SUPPORT_LOST" : "SUPPORT_GAINED"));
        }

        var recompetitionRequired = affectedCandidates.Count > 0 || affectedBranches.Count > 0 || essentialFailed.Count > 0;
        return new DependencyImpact(
            changedNodes, changedEdges, affectedBranches, affectedCandidates,
            essentialFailed, essentialUnverified, signals,
            RecompetitionRequired: recompetitionRequired,
            FrontierRecalculationRequired: recompetitionRequired);
    }

    // Walk lineage: a node's branch is either its own SourceBranchId or the nearest downstream
    // strategy/candidate node's branch reachable through outgoing edges (bounded, no string matching).
    private static Guid? ResolveLineageBranch(Model model, Node node)
    {
        if (node.SourceBranchId is { } direct)
            return direct;
        var seen = new HashSet<Guid> { node.Id };
        var queue = new Queue<Guid>();
        foreach (var e in model.OutgoingFrom(node.Id))
            queue.Enqueue(e.TargetId);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!seen.Add(id) || !model.Nodes.TryGetValue(id, out var n))
                continue;
            if (n.SourceBranchId is { } b)
                return b;
            foreach (var e in model.OutgoingFrom(id))
                queue.Enqueue(e.TargetId);
        }
        return null;
    }

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

        // 2. Essential dependencies satisfied. The gate is over essential NODES (the legal
        //    requirements the winning path depends on), never the edge count. The detail is phrased
        //    as "essential legal requirements" so the metric reads as a decision concept for the
        //    attorney rather than an arbitrary graph-structure count, and cannot be mistaken for an
        //    edge tally that merely coincides with the number of graph edges.
        var essentialNodes = model.Nodes.Values.Where(n => n.IsEssential).ToArray();
        var essentialOk = essentialNodes.All(n => n.IsSatisfied);
        Clause("Essential dependencies satisfied", essentialNodes.Length == 0 || essentialOk,
            "One or more essential dependencies are unsatisfied.",
            $"{essentialNodes.Count(n => n.IsSatisfied)}/{essentialNodes.Length} essential legal requirements satisfied.");

        // 3. Material authority verified. Distinguish the empty case (no material authority identified)
        //    from genuine full verification so we never report a vacuous "100% verified" on an empty set.
        //    Material authority means an authoritative SOURCE (evidence/citation) supports a fact or
        //    proposition; a bare Fact->Proposition dependency is NOT authority, so we require an Evidence
        //    source. Otherwise a graph with no authority nodes would falsely report "100% verified".
        var materialAuthority = model.Edges
            .Where(e => e.SourceKind == DecisionGraphNodeKinds.Evidence)
            .Where(e => e.TargetKind == DecisionGraphNodeKinds.Fact || e.TargetKind == DecisionGraphNodeKinds.Proposition)
            .Where(e => e.Materiality >= 0.5)
            .ToArray();
        if (materialAuthority.Length == 0)
        {
            // No material authority established yet: this is not a satisfied clause and not a "100%".
            Clause("Material authority verified", false,
                "No material authority has been established yet.",
                "Material authority: Not yet established.");
        }
        else
        {
            var verifiedFraction = (double)materialAuthority.Count(e => e.VerificationStatus == DecisionVerificationStates.Verified) / materialAuthority.Length;
            Clause("Material authority verified", verifiedFraction >= authorityVerifiedFractionRequired,
                "Material authority is not sufficiently verified.",
                $"{verifiedFraction:P0} of material authority verified.");
        }

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

        // 7. No high-impact open frontier. Label reflects the actual state (POLOXI owns the frontier
        //    count) so a failed clause never reads as the affirmative "No high-impact open frontier"
        //    and can never appear to contradict the authoritative frontier panel.
        var frontierOk = openHighImpactFrontierCount <= maxHighImpactFrontier;
        Clause(
            frontierOk ? "No high-impact open frontier" : $"{openHighImpactFrontierCount} high-impact open frontier item(s)",
            frontierOk,
            $"{openHighImpactFrontierCount} high-impact frontier item(s) still open.",
            frontierOk ? null : "Resolve open frontier branches before final reliance.");

        // 8. No dispositive verifier failure.
        var dispositiveFailure = model.Edges.Any(e => e.IsDispositive && e.VerificationStatus == DecisionVerificationStates.Invalidated);
        Clause("No dispositive verifier failure", !dispositiveFailure, "A dispositive edge was invalidated by the verifier.");

        return new DecisionReadinessVerdictDto(blockers.Count == 0, blockers, items);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
