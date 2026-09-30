using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// Shared POLOXI hierarchy-execution recording boundary. Both Wide search paths converge here AFTER
// POLOXI validation/acceptance to persist an accepted run as an authoritative Legal_HierarchyExecution.
// Persistence is CONTEXT-DRIVEN (not entry-path-driven): it runs only when a valid Decision Matter +
// Decision Contract + version context is present. Standalone/diagnostic runs skip persistence entirely.
// The write is fully fail-soft — a recording failure is logged and never affects the search response.
public sealed partial class IntelligenceWide2Service
{
    private const string HierarchyAlgorithmVersion = "POLOXI_WIDE_V3.21";

    // Maps an accepted WideBranchDto tree to the run-scoped node model and records the full graph.
    // Returns the persisted HierarchyExecutionId, or null when there is no decision context / on failure.
    private Task<Guid?> RecordAcceptedHierarchyAsync(
        Guid tenantId,
        Guid userId,
        Guid? decisionMatterId,
        Guid? decisionContractId,
        int? decisionContractVersion,
        string answerStatus,
        IReadOnlyCollection<WideBranchDto> branches,
        string? modelCode,
        string inputSnapshotHash,
        CancellationToken cancellationToken)
        => RecordAcceptedHierarchyCoreAsync(
            tenantId, userId, decisionMatterId, decisionContractId, decisionContractVersion,
            answerStatus, MapBranchesToNodes(branches), modelCode, inputSnapshotHash, cancellationToken);

    // Maps an accepted POLOXI (SearchWithPoloxiWideAsync) branch tree to nodes and records the full graph.
    private Task<Guid?> RecordAcceptedHierarchyAsync(
        Guid tenantId,
        Guid userId,
        Guid? decisionMatterId,
        Guid? decisionContractId,
        int? decisionContractVersion,
        IReadOnlyCollection<PoloxiBranchRecord> branches,
        string? modelCode,
        string inputSnapshotHash,
        CancellationToken cancellationToken)
        => RecordAcceptedHierarchyCoreAsync(
            tenantId, userId, decisionMatterId, decisionContractId, decisionContractVersion,
            "GROUNDED", MapPoloxiBranchesToNodes(branches), modelCode, inputSnapshotHash, cancellationToken);

    // Shared core: gate on decision context + acceptance, validate the graph, and record atomically.
    private async Task<Guid?> RecordAcceptedHierarchyCoreAsync(
        Guid tenantId,
        Guid userId,
        Guid? decisionMatterId,
        Guid? decisionContractId,
        int? decisionContractVersion,
        string answerStatus,
        IReadOnlyList<HierarchyNodeRecord> nodes,
        string? modelCode,
        string inputSnapshotHash,
        CancellationToken cancellationToken)
    {
        // Context-driven gate: only accepted runs inside a real Decision Contract context are recorded.
        if (hierarchyExecutionRepository is null
            || decisionMatterId is not { } matterId || matterId == Guid.Empty
            || decisionContractId is not { } contractId || contractId == Guid.Empty
            || decisionContractVersion is not { } contractVersion || contractVersion < 1)
            return null;

        // A run that ended asking the user to clarify is not an accepted hierarchy.
        if (string.Equals(answerStatus, "USER_CLARIFICATION_REQUIRED", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            // No accepted structure → nothing authoritative to persist (do not write an empty run).
            if (nodes.Count == 0)
                return null;

            var header = new RecordHierarchyExecutionCommand(
                DecisionMatterId: matterId,
                DecisionContractId: contractId,
                DecisionContractVersion: contractVersion,
                RunTypeCode: "INITIAL",
                ProcessingStatusCode: "GENERATED",
                ValidationStatusCode: "VALID",
                ModelCode: modelCode,
                ModelVersion: null,
                PromptCode: IntelligencePromptCodes.WideHierarchyStep,
                PromptVersion: 0,
                AlgorithmVersion: HierarchyAlgorithmVersion,
                ConfigurationVersion: null,
                InputSnapshotHash: inputSnapshotHash);

            // Edges and APR resolutions are not materialized by the current Wide pipeline; persist the
            // legitimate empty collections rather than synthesizing them.
            var record = new HierarchyExecutionRecord(
                header,
                nodes,
                Edges: [],
                Resolutions: []);

            // Establish internal graph consistency BEFORE the write transaction; a malformed accepted
            // structure is a pipeline defect and must not be persisted as a COMPLETE/VALID execution.
            var problems = HierarchyExecutionRecordValidator.Validate(record);
            if (problems.Count > 0)
            {
                logger.LogWarning(
                    "Skipping hierarchy execution recording for matter {MatterId}: {ProblemCount} consistency problem(s): {Problems}",
                    matterId, problems.Count, string.Join("; ", problems));
                return null;
            }

            var summary = await hierarchyExecutionRepository.RecordExecutionAsync(tenantId, userId, record, cancellationToken);
            return summary.HierarchyExecutionId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Fail-soft: recording must never break the primary search response.
            await errorLog.LogAsync("IntelligenceWide2Service", ex, nameof(RecordAcceptedHierarchyCoreAsync),
                cancellationToken: CancellationToken.None);
            return null;
        }
    }

    // Translates the accepted branch tree into run-scoped nodes. WideBranchId values are reused as the
    // node ids so intra-record parentage is preserved; Depth never determines NodeRoleCode.
    private static IReadOnlyList<HierarchyNodeRecord> MapBranchesToNodes(IReadOnlyCollection<WideBranchDto> branches)
    {
        if (branches.Count == 0)
            return [];

        var presentIds = branches.Select(b => b.WideBranchId).ToHashSet();

        return branches
            .OrderBy(b => b.LevelNumber)
            .ThenBy(b => b.SortOrder)
            .Select(b => new HierarchyNodeRecord(
                NodeId: b.WideBranchId,
                // Drop a dangling parent reference so the node becomes a valid root rather than an orphan.
                ParentNodeId: b.ParentWideBranchId is { } p && presentIds.Contains(p) ? p : null,
                Depth: b.LevelNumber < 1 ? 1 : b.LevelNumber,
                DisplayOrder: b.SortOrder,
                NodeTypeCode: b.SemanticTypeCode,
                NodeRoleCode: b.BranchRoleCode,
                Title: b.DisplayName,
                Statement: string.IsNullOrWhiteSpace(b.Interpretation) ? b.DisplayName : b.Interpretation,
                SearchText: b.SearchText,
                BranchStateCode: b.BranchStateCode,
                ContinueNarrowing: b.ContinueNarrowing,
                StopReasonCode: b.StopReason,
                Confidence: b.PoloxiConfidence,
                CapabilityCode: b.CapabilityCode,
                OriginCode: "LLM_PROPOSAL",
                OriginPromptCode: IntelligencePromptCodes.WideHierarchyStep,
                OriginPromptVersion: null,
                OriginModelCode: null,
                SemanticHash: null))
            .ToArray();
    }

    // Translates an accepted POLOXI branch tree into run-scoped nodes. HierarchyBranchId values are
    // reused as node ids so intra-record parentage is preserved. POLOXI branches expose no explicit
    // level; depth is derived from parent chains so the graph validator's contiguity holds.
    private static IReadOnlyList<HierarchyNodeRecord> MapPoloxiBranchesToNodes(IReadOnlyCollection<PoloxiBranchRecord> branches)
    {
        if (branches.Count == 0)
            return [];

        var presentIds = branches.Select(b => b.HierarchyBranchId).ToHashSet();
        var byId = branches.ToDictionary(b => b.HierarchyBranchId);

        int DepthOf(PoloxiBranchRecord b)
        {
            var depth = 1;
            var current = b;
            var guard = 0;
            while (current.ParentHierarchyBranchId is { } parentId
                   && presentIds.Contains(parentId)
                   && byId.TryGetValue(parentId, out var parent)
                   && guard++ < branches.Count)
            {
                depth++;
                current = parent;
            }
            return depth;
        }

        return branches
            .OrderBy(b => DepthOf(b))
            .ThenBy(b => b.SortOrder)
            .Select(b => new HierarchyNodeRecord(
                NodeId: b.HierarchyBranchId,
                ParentNodeId: b.ParentHierarchyBranchId is { } p && presentIds.Contains(p) ? p : null,
                Depth: DepthOf(b),
                DisplayOrder: b.SortOrder,
                NodeTypeCode: "PROPOSITION",
                NodeRoleCode: "BRANCH",
                Title: b.DisplayName,
                Statement: string.IsNullOrWhiteSpace(b.ProposedCondition) ? b.DisplayName : b.ProposedCondition,
                SearchText: b.SearchText,
                BranchStateCode: b.ValidationStatusCode,
                ContinueNarrowing: false,
                StopReasonCode: null,
                Confidence: b.Confidence,
                CapabilityCode: b.CapabilityCode,
                OriginCode: "LLM_PROPOSAL",
                OriginPromptCode: IntelligencePromptCodes.WideHierarchyStep,
                OriginPromptVersion: null,
                OriginModelCode: null,
                SemanticHash: null))
            .ToArray();
    }
}
