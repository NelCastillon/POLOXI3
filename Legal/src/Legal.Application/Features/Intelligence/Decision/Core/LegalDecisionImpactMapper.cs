using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2.1 — Impact → domain-neutral signal mapper (§8, §10, §27).
//
// Translates a DependencyImpact into signals keyed by AUTHORITATIVE POLOXI branch/candidate ids.
// The graph never carries legal vocabulary across this boundary — Core reacts to generic
// support/constraint/reopen deltas only. When lineage is absent on a node (e.g. the seeded 0215
// graph), the mapper degrades gracefully by matching a branch through its node code prefix against
// the authoritative branch codes (deterministic, never Query.Contains).
// ─────────────────────────────────────────────────────────────────────────────────────────────
public interface ILegalDecisionImpactMapper
{
    IReadOnlyList<DecisionBranchSignal> Map(
        DependencyImpact impact,
        DecisionGraphPersistence snapshot,
        IReadOnlyList<DecisionBranchPersistence> branches,
        IReadOnlyList<DecisionCandidatePersistence> candidates);
}

public sealed class LegalDecisionImpactMapper : ILegalDecisionImpactMapper
{
    public IReadOnlyList<DecisionBranchSignal> Map(
        DependencyImpact impact,
        DecisionGraphPersistence snapshot,
        IReadOnlyList<DecisionBranchPersistence> branches,
        IReadOnlyList<DecisionCandidatePersistence> candidates)
    {
        ArgumentNullException.ThrowIfNull(impact);
        if (impact.Signals.Count == 0)
            return [];

        var branchById = branches.ToDictionary(b => b.DecisionBranchId);
        var candidateById = candidates.ToDictionary(c => c.DecisionCandidateId);
        var mapped = new List<DecisionBranchSignal>();

        foreach (var signal in impact.Signals)
        {
            var branchId = signal.BranchId is { } b && branchById.ContainsKey(b)
                ? signal.BranchId
                : ResolveBranchFallback(signal, snapshot, branches);
            var candidateId = signal.CandidateId is { } c && candidateById.ContainsKey(c)
                ? signal.CandidateId
                : ResolveCandidateFromBranch(branchId, branches);

            // Drop signals that cannot be attached to any authoritative object (§10 — no phantom impact).
            if (branchId is null && candidateId is null)
                continue;

            mapped.Add(signal with { BranchId = branchId, CandidateId = candidateId });
        }

        return mapped;
    }

    // Lineage-first, then a deterministic branch-code prefix match (BuildResponse already labels
    // branches by candidate code e.g. "C1.B2"); never text/Query matching.
    private static Guid? ResolveBranchFallback(
        DecisionBranchSignal signal,
        DecisionGraphPersistence snapshot,
        IReadOnlyList<DecisionBranchPersistence> branches)
    {
        if (signal.CandidateId is { } cid)
        {
            var byCandidate = branches
                .Where(x => x.BranchCode.StartsWith(CandidatePrefix(cid, snapshot), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.AdvScore)
                .FirstOrDefault();
            if (byCandidate is not null)
                return byCandidate.DecisionBranchId;
        }
        // Highest-relevance frontier branch as a last resort so an essential failure is not lost.
        return branches
            .Where(x => x.IsOnFrontier)
            .OrderByDescending(x => x.DecisionRelevance)
            .FirstOrDefault()?.DecisionBranchId;
    }

    private static string CandidatePrefix(Guid candidateId, DecisionGraphPersistence snapshot)
    {
        var candidateNode = snapshot.Nodes.FirstOrDefault(n =>
            n.NodeKind == DecisionGraphNodeKinds.Candidate && (n.SourceCandidateId == candidateId || n.CandidateId == candidateId));
        return string.IsNullOrWhiteSpace(candidateNode?.NodeCode) ? "C" : candidateNode!.NodeCode + ".";
    }

    private static Guid? ResolveCandidateFromBranch(Guid? branchId, IReadOnlyList<DecisionBranchPersistence> branches)
    {
        // Candidate resolution is authoritative only through node lineage (SourceCandidateId).
        // Ordinal-only mapping from a branch code is unreliable, so we intentionally do not guess here.
        _ = branchId;
        _ = branches;
        return null;
    }
}
