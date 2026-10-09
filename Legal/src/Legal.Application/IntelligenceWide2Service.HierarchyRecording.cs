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
        // Context-driven gate: only accepted runs inside a real Decision Matter context are recorded.
        if (hierarchyExecutionRepository is null
            || decisionMatterId is not { } matterId || matterId == Guid.Empty)
            return null;

        // Server-side contract resolution (UI sends MatterId but no explicit contract context): when the
        // Decision Contract id/version are missing, resolve the matter's current authoritative contract
        // (ACTIVE if present, else latest DRAFT) so an accepted Wide run inside a matter still persists an
        // authoritative hierarchy execution. Explicitly-supplied contract context is always preserved.
        if (decisionContractId is not { } suppliedContractId || suppliedContractId == Guid.Empty
            || decisionContractVersion is not { } suppliedVersion || suppliedVersion < 1)
        {
            if (decisionContractRepository is null)
                return null;

            var current = await decisionContractRepository.GetCurrentContractAsync(tenantId, matterId, cancellationToken);
            if (current is null || current.DecisionContractId == Guid.Empty || current.VersionNumber < 1)
                return null;

            decisionContractId = current.DecisionContractId;
            decisionContractVersion = current.VersionNumber;
        }

        if (decisionContractId is not { } contractId || contractId == Guid.Empty
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

            // Auto-promote this accepted run to AUTHORITATIVE for its decision context so downstream readers
            // (LegalAuthority pass, Media Evidence, revision resolver, retrieval) resolve a current authority
            // immediately — recording a run alone never makes it authoritative. This mirrors the automatic
            // "latest accepted decision is authoritative" flow the channels expect; POLOXI remains the sole
            // evaluator. Promotion is fail-soft: a concurrency/state failure is logged and the recorded run id
            // is still returned so the response and channel ingestion proceed.
            try
            {
                await hierarchyExecutionRepository.PromoteAuthorityAsync(
                    tenantId, userId,
                    new PromoteHierarchyAuthorityCommand(
                        HierarchyExecutionId: summary.HierarchyExecutionId,
                        RowVersion: summary.RowVersion,
                        AuthorityReasonCode: "AUTO_WIDE_ACCEPTED"),
                    cancellationToken);
            }
            catch (Exception promoteEx) when (promoteEx is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(promoteEx,
                    "Recorded hierarchy execution {ExecutionId} for matter {MatterId} but auto-promotion to authority failed.",
                    summary.HierarchyExecutionId, matterId);
            }

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

    // Runs the registered Decision Channels (Document Evidence, Legal Authority, Human Intelligence,
    // Investigation, Decision Contract, External Research) against the freshly persisted hierarchy so their
    // VERIFIED contributions are produced and persisted for the matter. Mirrors the Document-Retrieval channel
    // flow: channels bind evidence to the authoritative hierarchy nodes; POLOXI Wide2 remains the sole scorer.
    // Also drives the standalone LegalAuthority pass so the Authority workspace populates automatically
    // (no manual "Run Authority Pass" button required). Both concerns are independent, context-gated, and
    // fully fail-soft — a missing dependency / hierarchy / matter simply skips, and any failure is logged
    // without ever affecting the primary search response.
    private async Task IngestChannelContributionsAsync(
        Guid tenantId,
        Guid userId,
        Guid? decisionMatterId,
        Guid? hierarchyExecutionId,
        CancellationToken cancellationToken)
    {
        if (decisionMatterId is not { } matterId || matterId == Guid.Empty
            || hierarchyExecutionId is not { } executionId || executionId == Guid.Empty)
            return;

        // 1) Decision-channel ingestion (binds verified evidence to the authoritative hierarchy nodes).
        if (channelOrchestrator is not null)
        {
            try
            {
                await channelOrchestrator.IngestContributionsAsync(tenantId, userId, matterId, executionId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Fail-soft: channel ingestion must never break the primary search response.
                await errorLog.LogAsync("IntelligenceWide2Service", ex, nameof(IngestChannelContributionsAsync),
                    cancellationToken: CancellationToken.None);
            }
        }

        // 2) Standalone LegalAuthority pass — same work the "Run Authority Pass" button performs, now run
        // automatically after a hierarchy is persisted so the Authority workspace populates without manual
        // action. Revisions are resolved server-side exactly like the controller endpoint.
        if (legalAuthorityOrchestration is not null && decisionRevisionResolver is not null)
        {
            try
            {
                var revisions = await decisionRevisionResolver.ResolveAsync(tenantId, matterId, cancellationToken);
                await legalAuthorityOrchestration.RunAsync(
                    new Abstractions.Intelligence.LegalAuthorityOrchestrationRequest(
                        tenantId,
                        userId,
                        matterId,
                        revisions.DecisionContractRevision,
                        revisions.CandidateSetRevision,
                        revisions.HierarchyRevision,
                        revisions.ScoringConfigurationVersion),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Fail-soft: the authority pass must never break the primary search response.
                await errorLog.LogAsync("IntelligenceWide2Service", ex, nameof(IngestChannelContributionsAsync),
                    cancellationToken: CancellationToken.None);
            }
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
