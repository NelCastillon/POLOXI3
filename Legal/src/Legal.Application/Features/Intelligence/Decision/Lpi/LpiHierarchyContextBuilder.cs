using System.Text.Json;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LpiHierarchyContextBuilder — turns an authoritative POLOXI hierarchy execution into the {{CONTEXT}}
// JSON the DECISION_EXTRACTION_V1 prompt matches placements against, plus a code→nodeId resolver.
//
// The extraction prompt proposes placements by `targetNodeCode`. To keep the contract authoritative and
// avoid lexical force-fitting, the builder emits each node's real HierarchyNodeId (GUID) as its
// `nodeCode`, so the extraction/accept path's existing Guid.TryParse resolves it directly. The resolver
// map is returned alongside so an orchestrator can validate that a proposed code names a real node.
//
// This is a pure projection: it NEVER scores nodes, assigns support, or ranks candidates.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class LpiHierarchyContextBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Candidate outcome node roles — the competing outcomes POLOXI keeps in play. Any node whose
    // role is not a candidate is treated as a reasoning/condition node.
    private static readonly HashSet<string> CandidateRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Candidate",
        "Outcome",
        "CandidateOutcome"
    };

    // Build the {{CONTEXT}} JSON string (candidates + nodes) and a code→HierarchyNodeId map from an
    // authoritative hierarchy execution's node lineage.
    public static LpiHierarchyContext Build(
        string decisionQuestion, IReadOnlyList<HierarchyNodeDto> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var resolver = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var candidateViews = new List<object>();
        var nodeViews = new List<object>();

        foreach (var node in nodes)
        {
            var code = node.HierarchyNodeId.ToString();
            resolver[code] = node.HierarchyNodeId;

            var label = string.IsNullOrWhiteSpace(node.Title) ? node.Statement : node.Title;
            var view = new
            {
                nodeCode = code,
                parentNodeCode = node.ParentHierarchyNodeId?.ToString(),
                depth = node.Depth,
                role = node.NodeRoleCode,
                label,
                statement = node.Statement,
                state = node.BranchStateCode
            };

            nodeViews.Add(view);
            if (CandidateRoles.Contains(node.NodeRoleCode))
                candidateViews.Add(new { nodeCode = code, label, statement = node.Statement });
        }

        var context = new
        {
            question = decisionQuestion,
            candidates = candidateViews,
            nodes = nodeViews
        };

        return new LpiHierarchyContext(
            JsonSerializer.Serialize(context, JsonOptions),
            resolver);
    }
}

// The {{CONTEXT}} JSON the extraction prompt consumes, plus the authoritative code→nodeId resolver so
// a caller can confirm a proposed placement names a real node rather than a hallucinated code.
public sealed record LpiHierarchyContext(
    string ContextJson,
    IReadOnlyDictionary<string, Guid> NodeResolver);
