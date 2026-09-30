namespace Legal.Application.Features.Intelligence.Decision;

// Pure-domain graph-consistency validation for a HierarchyExecutionRecord. This establishes that an
// accepted run is INTERNALLY valid (referential, acyclic, depth-consistent) BEFORE the repository
// opens a write transaction, so the persistence layer never becomes a second hierarchy validator —
// the DB then enforces referential integrity only as a final guard. Stateless and side-effect free.
public static class HierarchyExecutionRecordValidator
{
    // Returns an empty list when the record is internally consistent; otherwise the list of problems.
    public static IReadOnlyList<string> Validate(HierarchyExecutionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = new List<string>();

        var nodesById = new Dictionary<Guid, HierarchyNodeRecord>();
        foreach (var node in record.Nodes)
        {
            if (node.NodeId == Guid.Empty)
            {
                errors.Add("A node has an empty NodeId.");
                continue;
            }
            if (!nodesById.TryAdd(node.NodeId, node))
                errors.Add($"Duplicate NodeId '{node.NodeId}'.");
        }

        // Every ParentNodeId must exist; roots must be Depth 1; parent depth must be child depth - 1.
        foreach (var node in record.Nodes)
        {
            if (node.Depth < 1)
                errors.Add($"Node '{node.NodeId}' has invalid Depth {node.Depth} (must be >= 1).");

            if (node.ParentNodeId is { } parentId)
            {
                if (!nodesById.TryGetValue(parentId, out var parent))
                {
                    errors.Add($"Node '{node.NodeId}' references missing ParentNodeId '{parentId}'.");
                }
                else if (parent.NodeId == node.NodeId)
                {
                    errors.Add($"Node '{node.NodeId}' is its own parent.");
                }
                else if (parent.Depth != node.Depth - 1)
                {
                    errors.Add($"Node '{node.NodeId}' Depth {node.Depth} is inconsistent with parent Depth {parent.Depth}.");
                }
            }
            else if (node.Depth != 1)
            {
                errors.Add($"Root node '{node.NodeId}' must have Depth 1 but has Depth {node.Depth}.");
            }

            if (string.IsNullOrWhiteSpace(node.Statement))
                errors.Add($"Node '{node.NodeId}' has an empty Statement.");
        }

        // At least one root when the run has any nodes.
        if (record.Nodes.Count > 0 && !record.Nodes.Any(n => n.ParentNodeId is null))
            errors.Add("The hierarchy has nodes but no root (a node with no ParentNodeId).");

        // No cycles via parent chains.
        DetectCycles(record.Nodes, nodesById, errors);

        // Edge endpoints must exist and cannot be self-loops.
        foreach (var edge in record.Edges)
        {
            if (!nodesById.ContainsKey(edge.FromNodeId))
                errors.Add($"Edge references missing FromNodeId '{edge.FromNodeId}'.");
            if (!nodesById.ContainsKey(edge.ToNodeId))
                errors.Add($"Edge references missing ToNodeId '{edge.ToNodeId}'.");
            if (edge.FromNodeId == edge.ToNodeId)
                errors.Add($"Edge is a self-loop on node '{edge.FromNodeId}'.");
        }

        // APR resolutions must target an existing node with a unique (node, revision) pair.
        var resolutionKeys = new HashSet<(Guid, int)>();
        foreach (var resolution in record.Resolutions)
        {
            if (!nodesById.ContainsKey(resolution.NodeId))
                errors.Add($"Proposition resolution references missing NodeId '{resolution.NodeId}'.");
            if (!resolutionKeys.Add((resolution.NodeId, resolution.ResolutionRevision)))
                errors.Add($"Duplicate proposition resolution for node '{resolution.NodeId}' revision {resolution.ResolutionRevision}.");
        }

        return errors;
    }

    private static void DetectCycles(
        IReadOnlyList<HierarchyNodeRecord> nodes,
        IReadOnlyDictionary<Guid, HierarchyNodeRecord> nodesById,
        List<string> errors)
    {
        foreach (var start in nodes)
        {
            var seen = new HashSet<Guid>();
            var current = start;
            while (current.ParentNodeId is { } parentId)
            {
                if (!seen.Add(current.NodeId))
                {
                    errors.Add($"Cycle detected in parent chain starting at node '{start.NodeId}'.");
                    break;
                }
                if (!nodesById.TryGetValue(parentId, out var parent))
                    break; // missing-parent already reported
                current = parent;
            }
        }
    }
}
