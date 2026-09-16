using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2.1 — Dependency Propagation service.
//
// Wraps the deterministic DecisionGraph.PropagateImpact so the orchestrator depends on a small,
// explicit interface (§27). Rebuilds a working Model from a persisted graph snapshot (with lineage),
// applies a single edge verification change, and returns the structured DependencyImpact. Bounded by
// the configured propagation depth; deterministic and side-effect free on the DB (§6, §23, §36).
// ─────────────────────────────────────────────────────────────────────────────────────────────
public interface IDependencyPropagationService
{
    // Applies newStatus to the target edge in a working model built from the snapshot, then returns
    // the structured impact plus the mutated model (so the caller can persist the new graph state).
    DependencyPropagationOutcome Apply(
        DecisionGraphPersistence snapshot,
        Guid edgeId,
        string newStatus,
        int maxDepth);
}

public sealed record DependencyPropagationOutcome(
    DependencyImpact Impact,
    DecisionGraph.Model Model,
    string? PreviousStatus,
    bool EdgeFound);

public sealed class DependencyPropagationService : IDependencyPropagationService
{
    public DependencyPropagationOutcome Apply(DecisionGraphPersistence snapshot, Guid edgeId, string newStatus, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var model = BuildModel(snapshot);

        var edge = model.Edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge is null)
            return new DependencyPropagationOutcome(DependencyImpact.Empty, model, null, EdgeFound: false);

        var previous = edge.VerificationStatus;
        edge.VerificationStatus = NormalizeStatus(newStatus);

        // Recompute all node support first (idempotent), then produce the structured impact.
        foreach (var node in model.Nodes.Values)
            DecisionGraph.RecomputeSupport(model, node);

        var impact = DecisionGraph.PropagateImpact(model, maxDepth);
        return new DependencyPropagationOutcome(impact, model, previous, EdgeFound: true);
    }

    // Build the mutable working model from persisted nodes + edges, carrying V2.1 lineage across.
    public static DecisionGraph.Model BuildModel(DecisionGraphPersistence snapshot)
    {
        var model = new DecisionGraph.Model();
        foreach (var n in snapshot.Nodes)
        {
            model.Nodes[n.NodeId] = new DecisionGraph.Node
            {
                Id = n.NodeId,
                Kind = n.NodeKind,
                Code = n.NodeCode,
                DisplayName = n.DisplayName,
                Statement = n.Statement,
                Support = (double)n.Support,
                IsEssential = n.IsEssential,
                IsSatisfied = n.IsSatisfied,
                VerificationStatus = n.VerificationStatus,
                SortOrder = n.SortOrder,
                SourceBranchId = n.SourceBranchId,
                SourceCandidateId = n.SourceCandidateId ?? n.CandidateId,
                SourceEvidenceId = n.SourceEvidenceId,
                SourceAuthorityId = n.SourceAuthorityId
            };
        }
        foreach (var e in snapshot.Edges)
        {
            model.Edges.Add(new DecisionGraph.Edge
            {
                Id = e.EdgeId,
                Relation = e.RelationCode,
                SourceKind = e.SourceNodeKind,
                SourceId = e.SourceNodeId,
                TargetKind = e.TargetNodeKind,
                TargetId = e.TargetNodeId,
                SupportWeight = (double)e.SupportWeight,
                Materiality = (double)e.Materiality,
                IsEssential = e.IsEssential,
                IsDispositive = e.IsDispositive,
                VerificationStatus = e.VerificationStatus,
                PropagatedStateCode = e.PropagatedStateCode,
                AlternativePathAllowed = e.AlternativePathAllowed,
                PropagationPolicy = e.PropagationPolicy,
                SourceBranchId = e.SourceBranchId,
                SourceCandidateId = e.SourceCandidateId
            });
        }
        return model;
    }

    private static string NormalizeStatus(string status)
    {
        var s = (status ?? string.Empty).Trim().ToUpperInvariant();
        return s switch
        {
            DecisionVerificationStates.Verified => DecisionVerificationStates.Verified,
            DecisionVerificationStates.Invalidated => DecisionVerificationStates.Invalidated,
            _ => DecisionVerificationStates.Unverified
        };
    }
}
