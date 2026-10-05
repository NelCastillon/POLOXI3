using System.Data;
using System.Text.Json;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// DB-backed persistence for the shared LPI proposition-integration funnel (migration 0399).
//
// Backs POLOXI.Legal_RetrievedProposition / Legal_PropositionNodeLink / Legal_LpiCalculation /
// Legal_PropositionIntegrationOp. A single Apply commits the proposition, its placements, the optional
// advisory LPI record, the integration operation (with the unique idempotency key), a matter change
// event, and the outbox reassessment event — all in ONE serializable transaction. POLOXI Core owns
// outcome scoring; this repository never computes a score.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LpiPropositionIntegrationRepository(ISqlConnectionFactory connectionFactory)
    : ILpiPropositionIntegrationRepository
{
    public async Task<LpiIntegrationResult?> TryGetOperationAsync(
        Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<OpRow?>(new CommandDefinition(
            """
            SELECT TOP 1 RetrievedPropositionId, CommittedDecisionNodeId, SupersededPropositionIdsJson,
                   ChangeEventId, StatusCode, Explanation
            FROM POLOXI.Legal_PropositionIntegrationOp
            WHERE TenantId = @TenantId AND IdempotencyKey = @Key AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, Key = idempotencyKey },
            cancellationToken: cancellationToken));

        if (row is not { } op)
            return null;

        var superseded = string.IsNullOrWhiteSpace(op.SupersededPropositionIdsJson)
            ? []
            : JsonSerializer.Deserialize<List<Guid>>(op.SupersededPropositionIdsJson) ?? [];

        return new LpiIntegrationResult(
            Applied: op.CommittedDecisionNodeId is not null,
            CommittedPropositionId: op.RetrievedPropositionId,
            SupersededPropositionIds: superseded,
            ReassessmentEnqueued: op.ChangeEventId is not null,
            StatusCode: op.StatusCode,
            Explanation: op.Explanation ?? string.Empty);
    }

    public async Task<long> GetHierarchyVersionAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT ISNULL(MAX(NodeVersion), 0)
            FROM POLOXI.Legal_DecisionNode
            WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<string>> GetAcceptedPropositionTextsAsync(
        Guid tenantId, Guid decisionMatterId, IReadOnlyCollection<Guid> targetNodeIds,
        CancellationToken cancellationToken = default)
    {
        if (targetNodeIds.Count == 0)
            return [];

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var texts = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT p.PropositionText
            FROM POLOXI.Legal_RetrievedProposition p
            INNER JOIN POLOXI.Legal_PropositionNodeLink l ON l.RetrievedPropositionId = p.RetrievedPropositionId
            WHERE p.TenantId = @TenantId AND p.DecisionMatterId = @MatterId
              AND p.StateCode = N'Accepted' AND p.IsDeleted = 0 AND l.IsDeleted = 0
              AND l.TargetNodeId IN @TargetNodeIds;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId, TargetNodeIds = targetNodeIds },
            cancellationToken: cancellationToken));

        return texts.ToList();
    }

    public async Task<IReadOnlyList<LpiAncestorScore>> GetAncestorScoresAsync(
        Guid tenantId, Guid decisionMatterId, Guid parentNodeId, int maxDepth,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AncestorRow>(new CommandDefinition(
            """
            WITH Ancestors AS (
                SELECT n.DecisionNodeId, n.ParentNodeId, 1 AS Depth, n.NodeVersion,
                       COALESCE(ama.ConfirmedValue, n.EvaluatedValue) AS Value
                FROM POLOXI.Legal_DecisionNode n
                LEFT JOIN POLOXI.Legal_ApprovedMatterAssessment ama
                       ON ama.DecisionNodeId = n.DecisionNodeId AND ama.IsActive = 1 AND ama.IsDeleted = 0
                WHERE n.TenantId = @TenantId AND n.MatterId = @MatterId
                  AND n.DecisionNodeId = @ParentNodeId AND n.IsDeleted = 0
                UNION ALL
                SELECT pn.DecisionNodeId, pn.ParentNodeId, a.Depth + 1, pn.NodeVersion,
                       COALESCE(pama.ConfirmedValue, pn.EvaluatedValue) AS Value
                FROM POLOXI.Legal_DecisionNode pn
                INNER JOIN Ancestors a ON a.ParentNodeId = pn.DecisionNodeId
                LEFT JOIN POLOXI.Legal_ApprovedMatterAssessment pama
                       ON pama.DecisionNodeId = pn.DecisionNodeId AND pama.IsActive = 1 AND pama.IsDeleted = 0
                WHERE pn.TenantId = @TenantId AND pn.MatterId = @MatterId AND pn.IsDeleted = 0
                  AND a.Depth < @MaxDepth
            )
            SELECT DecisionNodeId, Depth, Value, NodeVersion
            FROM Ancestors
            WHERE Value IS NOT NULL
            ORDER BY Depth;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId, ParentNodeId = parentNodeId, MaxDepth = maxDepth },
            cancellationToken: cancellationToken));

        return rows.Select(r => new LpiAncestorScore(r.DecisionNodeId, r.Depth, r.Value, r.NodeVersion)).ToList();
    }

    public async Task<LpiIntegrationCommitResult> CommitIntegrationAsync(
        LpiIntegrationCommit commit, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var propositionId = Guid.NewGuid();
            var changeEventId = Guid.NewGuid();
            var opId = Guid.NewGuid();
            var p = commit.Proposition;
            var ctx = commit.Context;

            var supersededIds = commit.SupersedesPropositionId is { } s && s != Guid.Empty
                ? new List<Guid> { s }
                : [];

            // 1. RetrievedProposition (accepted) with exact provenance + attribution preserved.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_RetrievedProposition
                    (RetrievedPropositionId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId,
                     SourceLocator, SourceText, PropositionText, AssertionTypeCode, AttributedTo, EffectiveAtUtc,
                     RetrievalModeCode, StateCode, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @MatterId, @VersionId, NULL, @Locator, @SourceText, @PropositionText, @AssertionType,
                     @AttributedTo, @EffectiveAt, @Mode, N'Accepted', @TenantId, @Actor);
                """,
                new
                {
                    Id = propositionId, MatterId = commit.DecisionMatterId, VersionId = p.DocumentVersionId,
                    Locator = p.SourceLocator, SourceText = p.SourceText, PropositionText = p.PropositionText,
                    AssertionType = p.AssertionType.ToString(), AttributedTo = p.AttributedTo,
                    EffectiveAt = p.EffectiveAt?.UtcDateTime, Mode = LpiRetrievalMode.ConditionDirected.ToString(),
                    TenantId = commit.TenantId, Actor = commit.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 2. Placement links — qualitative relationship only; CONTEXT_ONLY carries no score.
            foreach (var placement in commit.Placements)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_PropositionNodeLink
                        (PropositionNodeLinkId, RetrievedPropositionId, DecisionMatterId, HierarchyRevision,
                         TargetNodeId, LeftNeighborId, RightNeighborId, PlacementFraction, RelationshipCode,
                         Rationale, StateCode, TenantId, CreatedByUserId)
                    VALUES
                        (@LinkId, @PropositionId, @MatterId, @HierarchyRevision, @TargetNodeId, @LeftNeighborId,
                         @RightNeighborId, @PlacementFraction, @Relationship, @Rationale, N'Accepted', @TenantId, @Actor);
                    """,
                    new
                    {
                        LinkId = Guid.NewGuid(), PropositionId = propositionId, MatterId = commit.DecisionMatterId,
                        HierarchyRevision = ctx.HierarchyRevision, placement.TargetNodeId,
                        LeftNeighborId = placement.LeftNeighborId, RightNeighborId = placement.RightNeighborId,
                        PlacementFraction = placement.Relationship == LpiRelationship.ContextOnly ? null : placement.PlacementFraction,
                        Relationship = placement.Relationship.ToString(), placement.Rationale,
                        TenantId = commit.TenantId, Actor = commit.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // 3. Optional advisory LPI calculation (only when the initializer produced one).
            if (commit.LpiCalculation is { } lpi)
            {
                var firstScoring = commit.Placements.FirstOrDefault(pl => pl.Relationship != LpiRelationship.ContextOnly);
                // The link id is regenerated per placement above; re-select it here is unnecessary — the
                // LPI record is matter/formula-scoped advisory metadata keyed to the proposition's first
                // scoring placement. We store it against a fresh link-independent row for auditability.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_LpiCalculation
                        (LpiCalculationId, PropositionNodeLinkId, DecisionMatterId, FormulaVersion, Alpha, Lambda,
                         MethodCode, LocalBaseline, AncestorContext, InitialScore, HasScore, AncestorsUsedJson,
                         Explanation, TenantId, CreatedByUserId)
                    SELECT TOP 1 @CalcId, l.PropositionNodeLinkId, @MatterId, @FormulaVersion, @Alpha, @Lambda,
                           @Method, @LocalBaseline, @AncestorContext, @InitialScore, @HasScore, @AncestorsJson,
                           @Explanation, @TenantId, @Actor
                    FROM POLOXI.Legal_PropositionNodeLink l
                    WHERE l.RetrievedPropositionId = @PropositionId AND l.TargetNodeId = @TargetNodeId AND l.IsDeleted = 0;
                    """,
                    new
                    {
                        CalcId = Guid.NewGuid(), PropositionId = propositionId, MatterId = commit.DecisionMatterId,
                        FormulaVersion = lpi.FormulaVersion, lpi.Alpha, lpi.Lambda, Method = lpi.Method.ToString(),
                        LocalBaseline = lpi.LocalBaseline, AncestorContext = lpi.AncestorContext, InitialScore = lpi.InitialScore,
                        HasScore = lpi.HasScore, AncestorsJson = JsonSerializer.Serialize(lpi.AncestorsUsed),
                        Explanation = lpi.Explanation, TargetNodeId = firstScoring?.TargetNodeId ?? Guid.Empty,
                        TenantId = commit.TenantId, Actor = commit.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // 4. Matter change event FIRST (impacts FK to it). Classification is relationship-aware: a
            //    CONTRADICTS placement is a MATERIAL_CONTRADICTION; otherwise a NEW_MATERIAL_FACT. The
            //    DecisionReevaluationDispatcher claims PROCESSED material events. Counts are backfilled
            //    after the impact rows are known.
            var anyContradiction = commit.Placements.Any(x => x.Relationship == LpiRelationship.Contradicts);
            var classification = anyContradiction ? "MATERIAL_CONTRADICTION" : "NEW_MATERIAL_FACT";
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_MatterChangeEvent
                    (MatterChangeEventId, DecisionMatterId, ChangeSourceCode, LegalDocumentVersionId, SourceLabel,
                     IdempotencyKey, ClassificationCode, ProcessingStatusCode, AffectedPropositionCount,
                     AffectedCandidateCount, ProcessedDateUtc, TenantId, CreatedByUserId)
                VALUES
                    (@ChangeEventId, @MatterId, N'PROPOSITION_INTEGRATION', @VersionId, @SourceLabel,
                     @Key, @Classification, N'PROCESSED', 0, 0, SYSUTCDATETIME(), @TenantId, @Actor);
                """,
                new
                {
                    ChangeEventId = changeEventId, MatterId = commit.DecisionMatterId, VersionId = p.DocumentVersionId,
                    SourceLabel = "Retrieved proposition integrated", Key = ctx.IdempotencyKey, Classification = classification,
                    TenantId = commit.TenantId, Actor = commit.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 5. Resolve node→candidate lineage and record Legal_DecisionImpact rows so the EXISTING
            //    impact-driven reassessment (DecisionReevaluationPlanner → DecisionRecompetition) has
            //    candidate-affecting signals to recompete. A placement with no authoritative owning
            //    candidate is UNRESOLVED (e.g. an LLM wide-branch node that is not an `adi:` candidate);
            //    it is flagged in the counts rather than silently treated as a successful integration.
            var resolvedCandidateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var propositionImpactCount = 0;
            var unresolvedPlacements = 0;

            // Per-candidate NET aggregation across ALL placements that reach it. A single source change can
            // reach one candidate from several placements; keying only on CandidateCode and keeping the FIRST
            // (Gap 3) silently discarded conflicting directions and was order-dependent. Instead we aggregate:
            // agreeing directions keep that direction; OPPOSING directions (one Strengthens, another Weakens)
            // are CONTESTED and net to the neutral RequiresEvaluation so the branch is reopened for attorney
            // evaluation with ZERO ranking bias — never an arbitrary first-wins pick. Severity escalates to
            // Material if any contributing placement is material. POLOXI Core still owns final magnitude.
            var candidateContributions = new Dictionary<string, CandidateDirectionAggregator>(StringComparer.OrdinalIgnoreCase);

            foreach (var placement in commit.Placements)
            {
                // CONTEXT_ONLY carries no numeric contribution — it never produces a candidate impact.
                if (placement.Relationship == LpiRelationship.ContextOnly || placement.TargetNodeId == Guid.Empty)
                    continue;

                // A. PROPOSITION-kind impact: the atomic proposition itself changed at this node.
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_DecisionImpact
                        (DecisionImpactId, MatterChangeEventId, DecisionMatterId, AffectedKindCode, AffectedKey,
                         AffectedLabel, PreviousStateCode, CurrentStateCode, ImpactSeverityCode, Rationale,
                         TenantId, CreatedByUserId)
                    VALUES
                        (@Id, @EventId, @MatterId, @Kind, @Key, @Label, NULL, @State, @Severity, @Rationale,
                         @TenantId, @Actor);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(), EventId = changeEventId, MatterId = commit.DecisionMatterId,
                        Kind = DecisionImpactKind.Proposition, Key = placement.TargetNodeId.ToString("N"),
                        Label = Truncate(p.PropositionText, 400), State = "Integrated",
                        Severity = PlacementSeverity(placement.Relationship), Rationale = placement.Rationale,
                        TenantId = commit.TenantId, Actor = commit.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
                propositionImpactCount++;

                // B. Resolve the owning session candidate(s) via bounded, cycle-safe lineage traversal.
                var lineage = await ResolveCandidateLineageAsync(
                    connection, tx, commit.TenantId, commit.DecisionMatterId, placement.TargetNodeId, cancellationToken);

                if (lineage.Count == 0)
                {
                    unresolvedPlacements++;
                    continue;
                }

                foreach (var owner in lineage)
                {
                    // NET per-candidate direction: relationship effect on the condition, inverted when the
                    // lineage path crosses a DEFEATING edge (supporting a defeating/defense condition
                    // WEAKENS the owning outcome). POLOXI Core still owns the final magnitude/competition.
                    var direction = ResolveNetDirection(placement.Relationship, owner.PathHasDefeating);
                    var isMaterial = string.Equals(
                        PlacementSeverity(placement.Relationship), DecisionImpactSeverity.Material, StringComparison.OrdinalIgnoreCase);
                    var basis = $"{placement.TargetNodeId:N}:{placement.Relationship}";

                    if (candidateContributions.TryGetValue(owner.CandidateCode, out var aggregator))
                        aggregator.Add(direction, isMaterial, basis);
                    else
                        candidateContributions[owner.CandidateCode] =
                            new CandidateDirectionAggregator(owner.CandidateCode, owner.CandidateLabel)
                                .Add(direction, isMaterial, basis);
                }
            }

            // Emit ONE candidate impact per owning candidate with the aggregated net direction. Contested
            // candidates (opposing contributions) resolve to RequiresEvaluation — reopened, never biased.
            foreach (var contribution in candidateContributions.Values)
            {
                resolvedCandidateKeys.Add(contribution.CandidateCode);
                var netDirection = contribution.ResolveNetDirection();
                var severity = contribution.IsMaterial ? DecisionImpactSeverity.Material : DecisionImpactSeverity.Potential;

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_DecisionImpact
                        (DecisionImpactId, MatterChangeEventId, DecisionMatterId, AffectedKindCode, AffectedKey,
                         AffectedLabel, PreviousStateCode, CurrentStateCode, ImpactSeverityCode, Rationale,
                         TenantId, CreatedByUserId)
                    VALUES
                        (@Id, @EventId, @MatterId, @Kind, @Key, @Label, @Previous, @State, @Severity, @Rationale,
                         @TenantId, @Actor);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(), EventId = changeEventId, MatterId = commit.DecisionMatterId,
                        Kind = DecisionImpactKind.Candidate, Key = contribution.CandidateCode,
                        Label = Truncate(contribution.CandidateLabel, 400), Previous = "Previously evaluated",
                        // CurrentStateCode encodes the already-resolved NET direction the planner reads.
                        State = netDirection, Severity = severity,
                        Rationale = Truncate(contribution.BuildRationale(netDirection), 2000),
                        TenantId = commit.TenantId, Actor = commit.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // 6. Backfill the change-event affected counts from the impact rows actually recorded.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_MatterChangeEvent
                SET AffectedPropositionCount = @PropCount, AffectedCandidateCount = @CandCount
                WHERE MatterChangeEventId = @ChangeEventId AND TenantId = @TenantId;
                """,
                new
                {
                    ChangeEventId = changeEventId, TenantId = commit.TenantId,
                    PropCount = propositionImpactCount, CandCount = resolvedCandidateKeys.Count
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 7. Integration operation with the unique idempotency key + reassessment linkage.
            //    CommittedDecisionNodeId is the decision node the proposition was committed against (the
            //    first placement's target), NOT the proposition id — it is null only on reject/withdraw.
            var committedNodeId = commit.Operation == LpiOperationKind.Withdraw
                ? (Guid?)null
                : commit.Placements.Select(pl => pl.TargetNodeId).FirstOrDefault() is { } tn && tn != Guid.Empty
                    ? tn
                    : null;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_PropositionIntegrationOp
                    (PropositionIntegrationOpId, DecisionMatterId, OriginCode, OperationCode, RetrievedPropositionId,
                     CommittedDecisionNodeId, DecisionContractRevision, CandidateSetRevision, HierarchyRevision,
                     SourceDocumentVersionId, ReviewerUserId, ScoringConfigurationVersion, IdempotencyKey,
                     SupersededPropositionIdsJson, ChangeEventId, StatusCode, Explanation, TenantId, CreatedByUserId)
                VALUES
                    (@OpId, @MatterId, N'Retrieval', @OperationCode, @PropositionId, @CommittedNodeId,
                     @ContractRev, @CandidateRev, @HierarchyRev, @VersionId, @Reviewer, @ScoringConfig, @Key,
                     @SupersededJson, @ChangeEventId, N'EvaluationPending', @Explanation, @TenantId, @Actor);
                """,
                new
                {
                    OpId = opId, MatterId = commit.DecisionMatterId, OperationCode = commit.Operation.ToString(),
                    PropositionId = propositionId, CommittedNodeId = committedNodeId, ContractRev = ctx.DecisionContractRevision,
                    CandidateRev = ctx.CandidateSetRevision, HierarchyRev = ctx.HierarchyRevision,
                    VersionId = ctx.SourceDocumentVersionId, Reviewer = ctx.ReviewerUserId,
                    ScoringConfig = ctx.ScoringConfigurationVersion, Key = ctx.IdempotencyKey,
                    SupersededJson = supersededIds.Count == 0 ? null : JsonSerializer.Serialize(supersededIds),
                    ChangeEventId = changeEventId,
                    Explanation = "Proposition integrated; POLOXI Core reassessment enqueued.",
                    TenantId = commit.TenantId, Actor = commit.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 6. Supersede prior propositions (revise/withdraw) — history preserved, never overwritten.
            if (supersededIds.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE POLOXI.Legal_RetrievedProposition
                    SET StateCode = N'Superseded', SupersededByPropositionId = @NewId,
                        ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                    WHERE TenantId = @TenantId AND RetrievedPropositionId IN @Ids AND IsDeleted = 0;
                    """,
                    new { NewId = propositionId, Actor = commit.ActorUserId, TenantId = commit.TenantId, Ids = supersededIds },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // NOTE: reassessment is triggered SOLELY by the Legal_MatterChangeEvent inserted above —
            // the hosted DecisionReevaluationHostedService polls PROCESSED + material events and runs the
            // existing impact-driven POLOXI recompute. We deliberately DO NOT enqueue a SaaS_Outbox
            // 'DecisionReevaluationRequested' row here: the generic OutboxDispatcher only handles
            // invitation emails and would repeatedly fail/retry an unknown message type. The change event
            // is the authoritative, consumed trigger; adding an outbox row would be redundant dead-letter.

            tx.Commit();
            return new LpiIntegrationCommitResult(
                propositionId, supersededIds, changeEventId,
                // Reassessment is "enqueued" (the change event will be claimed) ONLY when at least one
                // authoritative candidate impact exists for POLOXI Core to recompete. Zero resolved
                // candidates means the ranking cannot change from this proposition — the caller must NOT
                // present the old ranking as if it already reflects it.
                ReassessmentEnqueued: resolvedCandidateKeys.Count > 0,
                ResolvedCandidateCount: resolvedCandidateKeys.Count,
                UnresolvedPlacementCount: unresolvedPlacements);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── Bounded, cycle-safe node→session-candidate lineage resolver. ─────────────────────────────────
    // Climbs the Legal_DecisionNode parent chain from the placement's target node to the L1 Candidate
    // node, AND follows Dependency edges (REQUIRED | SUPPORTING | ALTERNATIVE | CONDITIONAL | DEFEATING)
    // in Legal_DecisionNodeEdge, scoped to tenant + matter. The L1 node's CanonicalKey embeds the session
    // candidate GUID (`adi:{DecisionCandidateId:N}:l1:{slug}`); we join Legal_DecisionCandidate for the
    // authoritative CandidateCode. Nodes that do not reach an `adi:` L1 candidate (e.g. `wb:` wide-branch
    // nodes) resolve to NOTHING and are reported as unresolved lineage — never guessed. MAXINT recursion
    // is capped and a visited set prevents cycles.
    public async Task<Guid> ParkForReviewAsync(
        LpiReviewPark park, CancellationToken cancellationToken = default)
    {
        // Park a proposition whose integration was BLOCKED (invalid gate or stale hierarchy) for attorney
        // triage instead of discarding it: the proposition + its placements are persisted with the
        // preserved review state (ReviewRequired | NeedsHierarchyReview) plus an integration-op row
        // carrying the idempotency key. Deliberately writes NO Legal_MatterChangeEvent and NO impacts: a
        // parked proposition has not changed the decision, so it must never trigger reassessment or let the
        // UI imply the ranking reflects it. Idempotent: a replay of the same (tenant, key) returns the id.
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var ctx = park.Context;

            var existing = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                """
                SELECT TOP 1 RetrievedPropositionId
                FROM POLOXI.Legal_PropositionIntegrationOp
                WHERE TenantId = @TenantId AND IdempotencyKey = @Key AND IsDeleted = 0;
                """,
                new { park.TenantId, Key = ctx.IdempotencyKey },
                transaction: tx, cancellationToken: cancellationToken));
            if (existing is { } alreadyParked)
            {
                tx.Commit();
                return alreadyParked;
            }

            var propositionId = Guid.NewGuid();
            var p = park.Proposition;

            // 1. RetrievedProposition parked with the preserved review state + reason (never 'Accepted').
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_RetrievedProposition
                    (RetrievedPropositionId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId,
                     SourceLocator, SourceText, PropositionText, AssertionTypeCode, AttributedTo, EffectiveAtUtc,
                     RetrievalModeCode, StateCode, StateReason, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @MatterId, @VersionId, NULL, @Locator, @SourceText, @PropositionText, @AssertionType,
                     @AttributedTo, @EffectiveAt, @Mode, @State, @Reason, @TenantId, @Actor);
                """,
                new
                {
                    Id = propositionId, MatterId = park.DecisionMatterId, VersionId = p.DocumentVersionId,
                    Locator = p.SourceLocator, SourceText = p.SourceText, PropositionText = p.PropositionText,
                    AssertionType = p.AssertionType.ToString(), AttributedTo = p.AttributedTo,
                    EffectiveAt = p.EffectiveAt?.UtcDateTime, Mode = LpiRetrievalMode.ConditionDirected.ToString(),
                    State = park.ReviewStateCode, Reason = Truncate(park.ReviewReason, 2000),
                    TenantId = park.TenantId, Actor = park.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 2. Placement links in the SAME preserved review state so the reviewer sees the proposed
            //    placements (placements may be empty when parked as NeedsHierarchyReview).
            foreach (var placement in park.Placements)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_PropositionNodeLink
                        (PropositionNodeLinkId, RetrievedPropositionId, DecisionMatterId, HierarchyRevision,
                         TargetNodeId, LeftNeighborId, RightNeighborId, PlacementFraction, RelationshipCode,
                         Rationale, StateCode, TenantId, CreatedByUserId)
                    VALUES
                        (@LinkId, @PropositionId, @MatterId, @HierarchyRevision, @TargetNodeId, @LeftNeighborId,
                         @RightNeighborId, @PlacementFraction, @Relationship, @Rationale, @State, @TenantId, @Actor);
                    """,
                    new
                    {
                        LinkId = Guid.NewGuid(), PropositionId = propositionId, MatterId = park.DecisionMatterId,
                        HierarchyRevision = ctx.HierarchyRevision, placement.TargetNodeId,
                        LeftNeighborId = placement.LeftNeighborId, RightNeighborId = placement.RightNeighborId,
                        PlacementFraction = placement.Relationship == LpiRelationship.ContextOnly ? null : placement.PlacementFraction,
                        Relationship = placement.Relationship.ToString(), placement.Rationale,
                        State = park.ReviewStateCode, TenantId = park.TenantId, Actor = park.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // 3. Integration op carrying the idempotency key, NO change event, terminal status 'Rejected'
            //    (the attempted integration was blocked; the proposition lives on for review).
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_PropositionIntegrationOp
                    (PropositionIntegrationOpId, DecisionMatterId, OriginCode, OperationCode, RetrievedPropositionId,
                     CommittedDecisionNodeId, DecisionContractRevision, CandidateSetRevision, HierarchyRevision,
                     SourceDocumentVersionId, ReviewerUserId, ScoringConfigurationVersion, IdempotencyKey,
                     SupersededPropositionIdsJson, ChangeEventId, StatusCode, Explanation, TenantId, CreatedByUserId)
                VALUES
                    (@OpId, @MatterId, N'Retrieval', @OperationCode, @PropositionId, NULL,
                     @ContractRev, @CandidateRev, @HierarchyRev, @VersionId, @Reviewer, @ScoringConfig, @Key,
                     NULL, NULL, N'Rejected', @Explanation, @TenantId, @Actor);
                """,
                new
                {
                    OpId = Guid.NewGuid(), MatterId = park.DecisionMatterId, OperationCode = park.Operation.ToString(),
                    PropositionId = propositionId, ContractRev = ctx.DecisionContractRevision,
                    CandidateRev = ctx.CandidateSetRevision, HierarchyRev = ctx.HierarchyRevision,
                    VersionId = ctx.SourceDocumentVersionId, Reviewer = ctx.ReviewerUserId,
                    ScoringConfig = ctx.ScoringConfigurationVersion, Key = ctx.IdempotencyKey,
                    Explanation = Truncate($"Parked for review ({park.ReviewStateCode}): {park.ReviewReason}", 2000),
                    TenantId = park.TenantId, Actor = park.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            tx.Commit();
            return propositionId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<LpiReviewItem>> GetPendingReviewItemsAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var propositions = await connection.QueryAsync<ReviewPropositionRow>(new CommandDefinition(
            """
            SELECT RetrievedPropositionId, DecisionMatterId, LegalDocumentVersionId, SourceLocator,
                   SourceText, PropositionText, AssertionTypeCode, AttributedTo, EffectiveAtUtc,
                   StateCode, StateReason
            FROM POLOXI.Legal_RetrievedProposition
            WHERE TenantId = @TenantId AND DecisionMatterId = @MatterId AND IsDeleted = 0
              AND StateCode IN (N'Extracted', N'PlacementProposed', N'ReviewRequired', N'NeedsHierarchyReview')
            ORDER BY CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId },
            cancellationToken: cancellationToken));

        var propositionList = propositions.ToList();
        if (propositionList.Count == 0)
            return [];

        var ids = propositionList.Select(p => p.RetrievedPropositionId).ToArray();
        var links = await connection.QueryAsync<ReviewLinkRow>(new CommandDefinition(
            """
            SELECT RetrievedPropositionId, TargetNodeId, LeftNeighborId, RightNeighborId,
                   PlacementFraction, RelationshipCode, Rationale, HierarchyRevision
            FROM POLOXI.Legal_PropositionNodeLink
            WHERE TenantId = @TenantId AND RetrievedPropositionId IN @Ids AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, Ids = ids },
            cancellationToken: cancellationToken));

        var linksByProposition = links
            .GroupBy(l => l.RetrievedPropositionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return propositionList.Select(p => ToReviewItem(p, linksByProposition)).ToList();
    }

    public async Task<LpiReviewItem?> GetReviewItemAsync(
        Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var proposition = await connection.QuerySingleOrDefaultAsync<ReviewPropositionRow>(new CommandDefinition(
            """
            SELECT RetrievedPropositionId, DecisionMatterId, LegalDocumentVersionId, SourceLocator,
                   SourceText, PropositionText, AssertionTypeCode, AttributedTo, EffectiveAtUtc,
                   StateCode, StateReason
            FROM POLOXI.Legal_RetrievedProposition
            WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, Id = retrievedPropositionId },
            cancellationToken: cancellationToken));
        if (proposition is null)
            return null;

        var links = await connection.QueryAsync<ReviewLinkRow>(new CommandDefinition(
            """
            SELECT RetrievedPropositionId, TargetNodeId, LeftNeighborId, RightNeighborId,
                   PlacementFraction, RelationshipCode, Rationale, HierarchyRevision
            FROM POLOXI.Legal_PropositionNodeLink
            WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, Id = retrievedPropositionId },
            cancellationToken: cancellationToken));

        var linksByProposition = links
            .GroupBy(l => l.RetrievedPropositionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return ToReviewItem(proposition, linksByProposition);
    }

    public async Task RejectReviewItemAsync(
        Guid tenantId, Guid actorUserId, Guid retrievedPropositionId, string reason,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_RetrievedProposition
                SET StateCode = N'Rejected', StateReason = @Reason,
                    ModifiedByUserId = @Actor, ModifiedDateUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0;

                UPDATE POLOXI.Legal_PropositionNodeLink
                SET StateCode = N'Rejected',
                    ModifiedByUserId = @Actor, ModifiedDateUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0;
                """,
                new { TenantId = tenantId, Id = retrievedPropositionId, Reason = Truncate(reason, 2000), Actor = actorUserId },
                transaction: tx, cancellationToken: cancellationToken));
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<LpiReviewItem?> GetAcceptedPropositionAsync(
        Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var proposition = await connection.QuerySingleOrDefaultAsync<ReviewPropositionRow>(new CommandDefinition(
            """
            SELECT RetrievedPropositionId, DecisionMatterId, LegalDocumentVersionId, SourceLocator,
                   SourceText, PropositionText, AssertionTypeCode, AttributedTo, EffectiveAtUtc,
                   StateCode, StateReason
            FROM POLOXI.Legal_RetrievedProposition
            WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0
              AND StateCode = N'Accepted';
            """,
            new { TenantId = tenantId, Id = retrievedPropositionId },
            cancellationToken: cancellationToken));
        if (proposition is null)
            return null;

        var links = await connection.QueryAsync<ReviewLinkRow>(new CommandDefinition(
            """
            SELECT RetrievedPropositionId, TargetNodeId, LeftNeighborId, RightNeighborId,
                   PlacementFraction, RelationshipCode, Rationale, HierarchyRevision
            FROM POLOXI.Legal_PropositionNodeLink
            WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0
              AND StateCode = N'Accepted';
            """,
            new { TenantId = tenantId, Id = retrievedPropositionId },
            cancellationToken: cancellationToken));

        var linksByProposition = links
            .GroupBy(l => l.RetrievedPropositionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return ToReviewItem(proposition, linksByProposition);
    }

    public async Task<LpiIntegrationCommitResult> WithdrawAcceptedAsync(
        LpiWithdrawCommit commit, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Idempotency: a replayed withdrawal (same op key) returns the original outcome.
            var existingOp = await connection.QuerySingleOrDefaultAsync<OpRow>(new CommandDefinition(
                """
                SELECT TOP 1 RetrievedPropositionId, CommittedDecisionNodeId, SupersededPropositionIdsJson,
                       ChangeEventId, StatusCode, Explanation
                FROM POLOXI.Legal_PropositionIntegrationOp
                WHERE TenantId = @TenantId AND IdempotencyKey = @Key AND IsDeleted = 0;
                """,
                new { TenantId = commit.TenantId, Key = commit.Context.IdempotencyKey },
                transaction: tx, cancellationToken: cancellationToken));
            if (existingOp is not null)
            {
                tx.Commit();
                return new LpiIntegrationCommitResult(
                    commit.RetrievedPropositionId, [commit.RetrievedPropositionId],
                    existingOp.ChangeEventId ?? Guid.Empty, ReassessmentEnqueued: existingOp.ChangeEventId is not null);
            }

            var changeEventId = Guid.NewGuid();
            var opId = Guid.NewGuid();
            var ctx = commit.Context;

            // 1. Mark the accepted proposition + its links Withdrawn — never deleted; history preserved.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_RetrievedProposition
                SET StateCode = N'Withdrawn', StateReason = @Reason,
                    ModifiedByUserId = @Actor, ModifiedDateUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0;

                UPDATE POLOXI.Legal_PropositionNodeLink
                SET StateCode = N'Withdrawn',
                    ModifiedByUserId = @Actor, ModifiedDateUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND RetrievedPropositionId = @Id AND IsDeleted = 0;
                """,
                new { TenantId = commit.TenantId, Id = commit.RetrievedPropositionId, Reason = Truncate(commit.Reason, 2000), Actor = commit.ActorUserId },
                transaction: tx, cancellationToken: cancellationToken));

            // 2. Matter change event: a withdrawal is a material change to the evidence set.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_MatterChangeEvent
                    (MatterChangeEventId, DecisionMatterId, ChangeSourceCode, LegalDocumentVersionId, SourceLabel,
                     IdempotencyKey, ClassificationCode, ProcessingStatusCode, AffectedPropositionCount,
                     AffectedCandidateCount, ProcessedDateUtc, TenantId, CreatedByUserId)
                VALUES
                    (@ChangeEventId, @MatterId, N'PROPOSITION_INTEGRATION', @VersionId, @SourceLabel,
                     @Key, N'MATERIAL_CONTRADICTION', N'PROCESSED', 0, 0, SYSUTCDATETIME(), @TenantId, @Actor);
                """,
                new
                {
                    ChangeEventId = changeEventId, MatterId = commit.DecisionMatterId,
                    VersionId = ctx.SourceDocumentVersionId, SourceLabel = "Retrieved proposition withdrawn",
                    Key = ctx.IdempotencyKey, TenantId = commit.TenantId, Actor = commit.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 3. Resolve node→candidate lineage per retracted placement and emit INVERTED candidate impacts
            //    so POLOXI Core recompetes WITHOUT the withdrawn proposition. Retracting SUPPORTS weakens
            //    the owning candidate; retracting CONTRADICTS strengthens it; QUALIFIES reopens evaluation.
            var resolvedCandidateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var propositionImpactCount = 0;
            var candidateContributions = new Dictionary<string, CandidateDirectionAggregator>(StringComparer.OrdinalIgnoreCase);

            foreach (var placement in commit.Placements)
            {
                if (placement.Relationship == LpiRelationship.ContextOnly || placement.TargetNodeId == Guid.Empty)
                    continue;

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_DecisionImpact
                        (DecisionImpactId, MatterChangeEventId, DecisionMatterId, AffectedKindCode, AffectedKey,
                         AffectedLabel, PreviousStateCode, CurrentStateCode, ImpactSeverityCode, Rationale,
                         TenantId, CreatedByUserId)
                    VALUES
                        (@Id, @EventId, @MatterId, @Kind, @Key, @Label, N'Integrated', N'Withdrawn', @Severity, @Rationale,
                         @TenantId, @Actor);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(), EventId = changeEventId, MatterId = commit.DecisionMatterId,
                        Kind = DecisionImpactKind.Proposition, Key = placement.TargetNodeId.ToString("N"),
                        Label = "Withdrawn proposition contribution retracted",
                        Severity = PlacementSeverity(placement.Relationship), Rationale = Truncate(commit.Reason, 400),
                        TenantId = commit.TenantId, Actor = commit.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
                propositionImpactCount++;

                var lineage = await ResolveCandidateLineageAsync(
                    connection, tx, commit.TenantId, commit.DecisionMatterId, placement.TargetNodeId, cancellationToken);

                foreach (var owner in lineage)
                {
                    // INVERT the normal direction: withdrawing a contribution removes its effect.
                    var normalDirection = ResolveNetDirection(placement.Relationship, owner.PathHasDefeating);
                    var retractedDirection = InvertDirection(normalDirection);
                    var isMaterial = string.Equals(
                        PlacementSeverity(placement.Relationship), DecisionImpactSeverity.Material, StringComparison.OrdinalIgnoreCase);
                    var basis = $"{placement.TargetNodeId:N}:Withdraw({placement.Relationship})";

                    if (candidateContributions.TryGetValue(owner.CandidateCode, out var aggregator))
                        aggregator.Add(retractedDirection, isMaterial, basis);
                    else
                        candidateContributions[owner.CandidateCode] =
                            new CandidateDirectionAggregator(owner.CandidateCode, owner.CandidateLabel)
                                .Add(retractedDirection, isMaterial, basis);
                }
            }

            foreach (var contribution in candidateContributions.Values)
            {
                resolvedCandidateKeys.Add(contribution.CandidateCode);
                var netDirection = contribution.ResolveNetDirection();
                var severity = contribution.IsMaterial ? DecisionImpactSeverity.Material : DecisionImpactSeverity.Potential;

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_DecisionImpact
                        (DecisionImpactId, MatterChangeEventId, DecisionMatterId, AffectedKindCode, AffectedKey,
                         AffectedLabel, PreviousStateCode, CurrentStateCode, ImpactSeverityCode, Rationale,
                         TenantId, CreatedByUserId)
                    VALUES
                        (@Id, @EventId, @MatterId, @Kind, @Key, @Label, @Previous, @State, @Severity, @Rationale,
                         @TenantId, @Actor);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(), EventId = changeEventId, MatterId = commit.DecisionMatterId,
                        Kind = DecisionImpactKind.Candidate, Key = contribution.CandidateCode,
                        Label = Truncate(contribution.CandidateLabel, 400), Previous = "Previously evaluated",
                        State = netDirection, Severity = severity,
                        Rationale = Truncate(contribution.BuildRationale(netDirection), 2000),
                        TenantId = commit.TenantId, Actor = commit.ActorUserId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // 4. Backfill the change-event affected counts.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_MatterChangeEvent
                SET AffectedPropositionCount = @PropCount, AffectedCandidateCount = @CandCount
                WHERE MatterChangeEventId = @ChangeEventId AND TenantId = @TenantId;
                """,
                new
                {
                    ChangeEventId = changeEventId, TenantId = commit.TenantId,
                    PropCount = propositionImpactCount, CandCount = resolvedCandidateKeys.Count
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 5. Withdraw integration op (no committed node; supersedes the withdrawn proposition id).
            var supersededIds = new List<Guid> { commit.RetrievedPropositionId };
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_PropositionIntegrationOp
                    (PropositionIntegrationOpId, DecisionMatterId, OriginCode, OperationCode, RetrievedPropositionId,
                     CommittedDecisionNodeId, DecisionContractRevision, CandidateSetRevision, HierarchyRevision,
                     SourceDocumentVersionId, ReviewerUserId, ScoringConfigurationVersion, IdempotencyKey,
                     SupersededPropositionIdsJson, ChangeEventId, StatusCode, Explanation, TenantId, CreatedByUserId)
                VALUES
                    (@OpId, @MatterId, N'Retrieval', N'Withdraw', @PropositionId, NULL,
                     @ContractRev, @CandidateRev, @HierarchyRev, @VersionId, @Reviewer, @ScoringConfig, @Key,
                     @SupersededJson, @ChangeEventId, N'EvaluationPending', @Explanation, @TenantId, @Actor);
                """,
                new
                {
                    OpId = opId, MatterId = commit.DecisionMatterId, PropositionId = commit.RetrievedPropositionId,
                    ContractRev = ctx.DecisionContractRevision, CandidateRev = ctx.CandidateSetRevision,
                    HierarchyRev = ctx.HierarchyRevision, VersionId = ctx.SourceDocumentVersionId,
                    Reviewer = ctx.ReviewerUserId, ScoringConfig = ctx.ScoringConfigurationVersion, Key = ctx.IdempotencyKey,
                    SupersededJson = JsonSerializer.Serialize(supersededIds), ChangeEventId = changeEventId,
                    Explanation = "Proposition withdrawn; POLOXI Core reassessment enqueued.",
                    TenantId = commit.TenantId, Actor = commit.ActorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            tx.Commit();
            return new LpiIntegrationCommitResult(
                commit.RetrievedPropositionId, supersededIds, changeEventId,
                ReassessmentEnqueued: resolvedCandidateKeys.Count > 0,
                ResolvedCandidateCount: resolvedCandidateKeys.Count,
                UnresolvedPlacementCount: 0);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // Retracting a contribution removes its effect: a Strengthened contribution becomes a Weakened net
    // effect when withdrawn, and vice versa. A neutral (RequiresEvaluation) qualifier simply reopens.
    private static string InvertDirection(string direction) => direction switch
    {
        Strengthened => Weakened,
        Weakened => Strengthened,
        _ => RequiresEvaluation,
    };

    private static LpiReviewItem ToReviewItem(
        ReviewPropositionRow p, IReadOnlyDictionary<Guid, List<ReviewLinkRow>> linksByProposition)
    {
        var placements = linksByProposition.TryGetValue(p.RetrievedPropositionId, out var rows)
            ? rows.Select(l => new LpiReviewPlacement(
                l.TargetNodeId,
                l.LeftNeighborId,
                l.RightNeighborId,
                l.PlacementFraction,
                l.RelationshipCode,
                l.Rationale ?? string.Empty,
                l.HierarchyRevision)).ToList()
            : [];

        return new LpiReviewItem(
            p.RetrievedPropositionId,
            p.DecisionMatterId,
            p.LegalDocumentVersionId,
            p.SourceLocator,
            p.SourceText,
            p.PropositionText,
            p.AssertionTypeCode,
            p.AttributedTo,
            p.EffectiveAtUtc is { } e ? new DateTimeOffset(DateTime.SpecifyKind(e, DateTimeKind.Utc)) : null,
            p.StateCode,
            p.StateReason,
            placements);
    }

    private static async Task<IReadOnlyList<CandidateLineage>> ResolveCandidateLineageAsync(
        IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid matterId, Guid targetNodeId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<LineageRow>(new CommandDefinition(
            """
            WITH Lineage AS (
                -- Anchor: the placement target node itself (depth 0, no defeating crossing yet).
                SELECT n.DecisionNodeId, n.ParentNodeId, n.CanonicalKey, n.NodeKindCode,
                       0 AS Depth, CAST(0 AS BIT) AS PathHasDefeating
                FROM POLOXI.Legal_DecisionNode n
                WHERE n.TenantId = @TenantId AND n.MatterId = @MatterId
                  AND n.DecisionNodeId = @TargetNodeId AND n.IsDeleted = 0
                UNION ALL
                -- Structural parent climb.
                SELECT pn.DecisionNodeId, pn.ParentNodeId, pn.CanonicalKey, pn.NodeKindCode,
                       l.Depth + 1, l.PathHasDefeating
                FROM POLOXI.Legal_DecisionNode pn
                INNER JOIN Lineage l ON l.ParentNodeId = pn.DecisionNodeId
                WHERE pn.TenantId = @TenantId AND pn.MatterId = @MatterId AND pn.IsDeleted = 0
                  AND l.Depth < 16 AND l.NodeKindCode <> N'Candidate'
                UNION ALL
                -- Dependency-edge traversal (FROM depends on / supports / defeats TO). A DEFEATING edge on
                -- the path inverts the owning-candidate polarity downstream.
                SELECT tn.DecisionNodeId, tn.ParentNodeId, tn.CanonicalKey, tn.NodeKindCode,
                       l.Depth + 1,
                       CASE WHEN e.EdgeTypeCode = N'DEFEATING' THEN CAST(1 AS BIT) ELSE l.PathHasDefeating END
                FROM POLOXI.Legal_DecisionNodeEdge e
                INNER JOIN Lineage l ON l.DecisionNodeId = e.FromNodeId
                INNER JOIN POLOXI.Legal_DecisionNode tn ON tn.DecisionNodeId = e.ToNodeId
                WHERE e.TenantId = @TenantId AND e.MatterId = @MatterId AND e.IsDeleted = 0
                  AND e.EdgeKindCode = N'Dependency' AND tn.IsDeleted = 0
                  AND l.Depth < 16 AND l.NodeKindCode <> N'Candidate'
            )
            SELECT DISTINCT c.CandidateCode, c.DisplayName AS CandidateLabel, l.PathHasDefeating
            FROM Lineage l
            INNER JOIN POLOXI.Legal_DecisionCandidate c
                    ON c.DecisionCandidateId = TRY_CONVERT(UNIQUEIDENTIFIER,
                         SUBSTRING(l.CanonicalKey, 5, 32))
                   AND c.IsDeleted = 0
            WHERE l.NodeKindCode = N'Candidate' AND l.CanonicalKey LIKE N'adi:%'
            OPTION (MAXRECURSION 64);
            """,
            new { TenantId = tenantId, MatterId = matterId, TargetNodeId = targetNodeId },
            transaction: tx, cancellationToken: cancellationToken));

        // Collapse to one lineage per candidate; a candidate reached by ANY defeating path is treated as
        // defeating (the most conservative inversion).
        return rows
            .GroupBy(r => r.CandidateCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CandidateLineage(
                g.Key,
                g.Select(x => x.CandidateLabel).FirstOrDefault(),
                g.Any(x => x.PathHasDefeating)))
            .ToList();
    }

    // SUPPORTS strengthens the owning candidate; CONTRADICTS weakens it. A DEFEATING edge on the lineage
    // path inverts that net effect. QUALIFIES is NOT support: it conditions/narrows an outcome, so it
    // carries NO fabricated direction — it resolves to a neutral RequiresEvaluation state that triggers
    // attorney evaluation without biasing the ranking. These tokens are read by DecisionReevaluationPlanner,
    // which applies POLOXI's severity-scaled magnitude — the LPI value is NEVER added directly to an outcome.
    private const string Strengthened = "Strengthened";
    private const string Weakened = "Weakened";
    private const string RequiresEvaluation = "RequiresEvaluation";

    private static string ResolveNetDirection(LpiRelationship relationship, bool pathHasDefeating)
    {
        // A qualifier narrows/conditions the outcome (e.g. "liable, BUT only up to the policy limit"). It
        // is neither support nor contradiction, so it never produces a signed signal; a DEFEATING edge
        // cannot flip a neutral into a direction either. POLOXI Core / attorney review resolves its effect.
        if (relationship == LpiRelationship.Qualifies)
            return RequiresEvaluation;

        var baseStrengthens = relationship != LpiRelationship.Contradicts; // Supports strengthen
        var strengthens = pathHasDefeating ? !baseStrengthens : baseStrengthens;
        return strengthens ? Strengthened : Weakened;
    }

    // CONTRADICTS is a material contradiction; Supports/Qualifies are potential (non-material) changes.
    private static string PlacementSeverity(LpiRelationship relationship)
        => relationship == LpiRelationship.Contradicts
            ? DecisionImpactSeverity.Material
            : DecisionImpactSeverity.Potential;

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];

    private sealed record LineageRow(string CandidateCode, string? CandidateLabel, bool PathHasDefeating);

    private sealed record CandidateLineage(string CandidateCode, string? CandidateLabel, bool PathHasDefeating);

    private sealed record OpRow(
        Guid RetrievedPropositionId,
        Guid? CommittedDecisionNodeId,
        string? SupersededPropositionIdsJson,
        Guid? ChangeEventId,
        string StatusCode,
        string? Explanation);

    private sealed record AncestorRow(Guid DecisionNodeId, int Depth, decimal Value, long NodeVersion);

    private sealed record ReviewPropositionRow(
        Guid RetrievedPropositionId,
        Guid DecisionMatterId,
        Guid LegalDocumentVersionId,
        string SourceLocator,
        string SourceText,
        string PropositionText,
        string AssertionTypeCode,
        string? AttributedTo,
        DateTime? EffectiveAtUtc,
        string StateCode,
        string? StateReason);

    private sealed record ReviewLinkRow(
        Guid RetrievedPropositionId,
        Guid TargetNodeId,
        Guid? LeftNeighborId,
        Guid? RightNeighborId,
        decimal? PlacementFraction,
        string RelationshipCode,
        string? Rationale,
        long HierarchyRevision);
}
