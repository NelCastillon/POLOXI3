using System.Data;
using System.Text.Json;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// DB-backed reads for the Attorney Decision Input (Human Intelligence) tab. Tenant-scoped on every
// query. POLOXI remains the authoritative evaluator; this repository never computes or mutates scores.
public sealed class AttorneyDecisionInputRepository(ISqlConnectionFactory connectionFactory) : IAttorneyDecisionInputRepository
{
    public async Task<MatterHumanIntelligenceDto> GetMatterHumanIntelligenceAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var enabled = await IsFeatureEnabledAsync(connection, cancellationToken);

        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            -- Nodes
            SELECT node.DecisionNodeId, node.ParentNodeId, node.CanonicalKey, node.NodeKindCode,
                   node.NodeLevel, node.NodeText, node.OriginCode, node.PlacementKey,
                   node.StructuralStateCode, node.EvidenceStateCode, node.AuthorityStateCode, node.EvaluatedValue, node.NodeVersion,
                   (SELECT COUNT(1) FROM POLOXI.Legal_AttorneyDecisionChallenge challenge
                     WHERE challenge.DecisionNodeId = node.DecisionNodeId AND challenge.IsDeleted = 0
                       AND challenge.StatusCode IN (N'Open', N'UnderReview')) AS OpenChallengeCount
            FROM POLOXI.Legal_DecisionNode node
            WHERE node.TenantId = @TenantId AND node.MatterId = @MatterId AND node.IsDeleted = 0
            ORDER BY node.NodeLevel, node.PlacementKey, node.CanonicalKey;

            -- Assessments (with attorney display name / role)
            SELECT assessment.AssessmentId, assessment.DecisionNodeId, assessment.AttorneyUserId,
                   LTRIM(RTRIM(CONCAT(ISNULL(u.FirstName, N''), N' ', ISNULL(u.LastName, N'')))) AS AttorneyDisplayName,
                   role.DisplayName AS AttorneyRole,
                   assessment.ConfirmedValue, assessment.SuggestedMidpoint,
                   assessment.PreviousSiblingId, assessment.PreviousSiblingValue,
                   assessment.NextSiblingId, assessment.NextSiblingValue,
                   assessment.MethodCode, assessment.Rationale, assessment.StatusCode,
                   assessment.AssessmentVersion, assessment.CreatedDateUtc
            FROM POLOXI.Legal_AttorneyRelativeAssessment assessment
            LEFT JOIN dbo.AspNetUsers u ON u.Id = assessment.AttorneyUserId
            OUTER APPLY (
                SELECT TOP 1 r.DisplayName
                FROM SaaS.SaaS_TenantMembership m
                JOIN SaaS.SaaS_Role r ON r.RoleId = m.RoleId AND r.IsDeleted = 0
                WHERE m.UserId = assessment.AttorneyUserId AND m.TenantId = @TenantId AND m.IsDeleted = 0
            ) role
            WHERE assessment.TenantId = @TenantId AND assessment.MatterId = @MatterId AND assessment.IsDeleted = 0
            ORDER BY assessment.CreatedDateUtc;

            -- Approved assessments (single active per node)
            SELECT approval.ApprovalId, approval.DecisionNodeId, approval.AssessmentId,
                   assessment.ConfirmedValue,
                   approval.ApprovedByUserId,
                   LTRIM(RTRIM(CONCAT(ISNULL(u.FirstName, N''), N' ', ISNULL(u.LastName, N'')))) AS ApprovedByDisplayName,
                   approval.GovernancePolicyCode, approval.CreatedDateUtc AS ApprovedDateUtc,
                   assessment.PreviousSiblingValue, assessment.NextSiblingValue
            FROM POLOXI.Legal_ApprovedMatterAssessment approval
            JOIN POLOXI.Legal_AttorneyRelativeAssessment assessment ON assessment.AssessmentId = approval.AssessmentId
            LEFT JOIN dbo.AspNetUsers u ON u.Id = approval.ApprovedByUserId
            WHERE approval.TenantId = @TenantId AND approval.MatterId = @MatterId
              AND approval.IsActive = 1 AND approval.IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = matterId },
            cancellationToken: cancellationToken));

        var nodeRows = (await multi.ReadAsync<NodeRow>()).ToList();
        var assessmentRows = (await multi.ReadAsync<AttorneyRelativeAssessmentDto>()).ToList();
        var approvalRows = (await multi.ReadAsync<ApprovedMatterAssessmentDto>()).ToList();

        var assessmentsByNode = assessmentRows
            .GroupBy(a => a.DecisionNodeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AttorneyRelativeAssessmentDto>)g.ToArray());
        var approvalByNode = approvalRows
            .GroupBy(a => a.DecisionNodeId)
            .ToDictionary(g => g.Key, g => g.First());

        var nodes = nodeRows.Select(row => new AttorneyDecisionNodeDto(
                row.DecisionNodeId, row.ParentNodeId, row.CanonicalKey, row.NodeKindCode, row.NodeLevel,
                row.NodeText, row.OriginCode, row.PlacementKey, row.StructuralStateCode, row.EvidenceStateCode,
                row.AuthorityStateCode, row.EvaluatedValue,
                assessmentsByNode.TryGetValue(row.DecisionNodeId, out var a) ? a : [],
                approvalByNode.TryGetValue(row.DecisionNodeId, out var ap) ? ap : null,
                row.OpenChallengeCount,
                row.NodeVersion))
            .ToArray();

        return new MatterHumanIntelligenceDto(
            matterId,
            enabled,
            nodes.Length,
            assessmentRows.Select(a => a.AttorneyUserId).Distinct().Count(),
            assessmentRows.Count,
            approvalRows.Count,
            nodeRows.Sum(n => n.OpenChallengeCount),
            nodes);
    }

    private static async Task<bool> IsFeatureEnabledAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var value = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 SettingValue FROM POLOXI.Legal_DecisionSetting WHERE SettingKey = N'AttorneyDecisionInput.Enabled' AND IsDeleted = 0;",
            cancellationToken: cancellationToken));
        // Human Intelligence (Attorney Decision Input) defaults ON: absent/unparseable setting => enabled;
        // only an explicit stored 'false' disables it.
        return !bool.TryParse(value, out var parsed) || parsed;
    }

    // ── Placement / sibling reads (§4) ───────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<(Guid NodeId, string NodeText, decimal? Value)>> GetSiblingValuesAsync(
        Guid tenantId, Guid matterId, Guid candidateNodeId, Guid? parentNodeId, int nodeLevel,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SiblingRow>(new CommandDefinition(
            """
            SELECT n.DecisionNodeId, n.NodeText,
                   COALESCE(ama.ConfirmedValue, n.EvaluatedValue) AS Value
            FROM POLOXI.Legal_DecisionNode n
            OUTER APPLY (
                SELECT TOP 1 a.ConfirmedValue
                FROM POLOXI.Legal_ApprovedMatterAssessment ap
                JOIN POLOXI.Legal_AttorneyRelativeAssessment a ON a.AssessmentId = ap.AssessmentId
                WHERE ap.DecisionNodeId = n.DecisionNodeId AND ap.IsActive = 1 AND ap.IsDeleted = 0
            ) ama
            WHERE n.TenantId = @TenantId AND n.MatterId = @MatterId AND n.IsDeleted = 0
              AND n.NodeLevel = @NodeLevel
              AND ((@ParentNodeId IS NULL AND n.ParentNodeId IS NULL) OR n.ParentNodeId = @ParentNodeId)
            ORDER BY n.PlacementKey, n.CanonicalKey;
            """,
            new { TenantId = tenantId, MatterId = matterId, ParentNodeId = parentNodeId, NodeLevel = nodeLevel },
            cancellationToken: cancellationToken));
        return rows.Select(r => (r.DecisionNodeId, r.NodeText, r.Value)).ToArray();
    }

    public async Task<IReadOnlyList<AttorneyDuplicateCandidateDto>> FindDuplicateCandidatesAsync(
        Guid tenantId, Guid matterId, string nodeText, CancellationToken cancellationToken = default)
    {
        var normalized = (nodeText ?? string.Empty).Trim();
        if (normalized.Length == 0) return [];
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Deterministic candidate shortlist: exact/prefix/contains match on normalized text within matter.
        var rows = await connection.QueryAsync<DuplicateRow>(new CommandDefinition(
            """
            SELECT TOP 5 n.DecisionNodeId, n.CanonicalKey, n.NodeText
            FROM POLOXI.Legal_DecisionNode n
            WHERE n.TenantId = @TenantId AND n.MatterId = @MatterId AND n.IsDeleted = 0
              AND (LOWER(n.NodeText) = LOWER(@Text)
                   OR LOWER(n.NodeText) LIKE LOWER(@Prefix)
                   OR LOWER(@Text) LIKE LOWER('%' + n.NodeText + '%'))
            ORDER BY CASE WHEN LOWER(n.NodeText) = LOWER(@Text) THEN 0 ELSE 1 END, LEN(n.NodeText);
            """,
            new { TenantId = tenantId, MatterId = matterId, Text = normalized, Prefix = normalized + "%" },
            cancellationToken: cancellationToken));
        return rows.Select(r => new AttorneyDuplicateCandidateDto(
            r.DecisionNodeId, r.CanonicalKey, r.NodeText,
            string.Equals(r.NodeText?.Trim(), normalized, StringComparison.OrdinalIgnoreCase) ? 1.0m : 0.75m)).ToArray();
    }

    public async Task<long> GetHierarchyVersionAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT ISNULL(SUM(CONVERT(BIGINT, NodeVersion)), 0)
            FROM POLOXI.Legal_DecisionNode
            WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken));
    }

    public async Task<CommitResult?> TryGetCommittedAsync(Guid tenantId, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var json = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 DetailJson
            FROM POLOXI.Legal_DecisionNodeAudit
            WHERE TenantId = @TenantId AND ActionCode = N'NodeAdded'
              AND CausationId = @Key AND IsDeleted = 0
            ORDER BY CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, Key = idempotencyKey }, cancellationToken: cancellationToken));
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<CommitResult>(json); }
        catch { return null; }
    }

    // ── §16 transactional commit ─────────────────────────────────────────────────────────────────
    public async Task<CommitResult> CommitAttorneyInputAsync(
        Guid tenantId, Guid actorUserId, CommitAttorneyInputCommand command, string canonicalKey, string placementKey,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var nodeId = Guid.NewGuid();
            var assessmentId = Guid.NewGuid();
            var changeEventId = Guid.NewGuid();
            Guid? approvalId = null;

            // 1. Persist canonical node (Origin = AttorneySupplied — origin alone never adds points, §13).
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionNode
                    (DecisionNodeId, MatterId, ParentNodeId, CanonicalKey, NodeKindCode, NodeLevel, NodeText,
                     OriginCode, NodeVersion, StructuralStateCode, EvidenceStateCode, AuthorityStateCode,
                     PlacementKey, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionNodeId, @MatterId, @ParentNodeId, @CanonicalKey, @NodeKindCode, @NodeLevel, @NodeText,
                     N'AttorneySupplied', 1, N'Proposed', N'NotEvaluated', N'NotRequired',
                     @PlacementKey, @TenantId, @Actor);
                """,
                new
                {
                    DecisionNodeId = nodeId, command.MatterId, command.ParentNodeId, CanonicalKey = canonicalKey,
                    command.NodeKindCode, command.NodeLevel, command.NodeText, PlacementKey = placementKey,
                    TenantId = tenantId, Actor = actorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 2. Persist the attorney relative assessment for the new node.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AttorneyRelativeAssessment
                    (AssessmentId, MatterId, DecisionNodeId, AttorneyUserId, ConfirmedValue, SuggestedMidpoint,
                     PreviousSiblingId, PreviousSiblingValue, NextSiblingId, NextSiblingValue, DecisionSnapshotId,
                     MethodCode, Rationale, StatusCode, AssessmentVersion, TenantId, CreatedByUserId)
                VALUES
                    (@AssessmentId, @MatterId, @DecisionNodeId, @Actor, @ConfirmedValue, @SuggestedMidpoint,
                     @PreviousSiblingId, @PreviousSiblingValue, @NextSiblingId, @NextSiblingValue, @BaseSnapshotId,
                     @MethodCode, @Rationale, @StatusCode, 1, @TenantId, @Actor);
                """,
                new
                {
                    AssessmentId = assessmentId, command.MatterId, DecisionNodeId = nodeId, Actor = actorUserId,
                    command.ConfirmedValue, command.SuggestedMidpoint, command.PreviousSiblingId, command.PreviousSiblingValue,
                    command.NextSiblingId, command.NextSiblingValue, command.BaseSnapshotId, command.MethodCode,
                    command.Rationale, StatusCode = command.RequestApproval ? "Submitted" : "Current",
                    TenantId = tenantId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 3. Optional self-approval when governance policy authorizes the committing attorney (§5).
            if (command.RequestApproval && !string.IsNullOrWhiteSpace(command.GovernancePolicyCode))
            {
                approvalId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE POLOXI.Legal_ApprovedMatterAssessment
                    SET IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                    WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionNodeId = @DecisionNodeId AND IsActive = 1 AND IsDeleted = 0;

                    INSERT INTO POLOXI.Legal_ApprovedMatterAssessment
                        (ApprovalId, MatterId, DecisionNodeId, AssessmentId, ApprovedByUserId, GovernancePolicyCode,
                         DecisionSnapshotId, ApprovalVersion, IsActive, TenantId, CreatedByUserId)
                    VALUES
                        (@ApprovalId, @MatterId, @DecisionNodeId, @AssessmentId, @Actor, @GovernancePolicyCode,
                         @BaseSnapshotId, 1, 1, @TenantId, @Actor);

                    UPDATE POLOXI.Legal_AttorneyRelativeAssessment
                    SET StatusCode = N'ApprovedForMatter', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                    WHERE AssessmentId = @AssessmentId;
                    """,
                    new
                    {
                        ApprovalId = approvalId, command.MatterId, DecisionNodeId = nodeId, AssessmentId = assessmentId,
                        Actor = actorUserId, command.GovernancePolicyCode, command.BaseSnapshotId, TenantId = tenantId
                    },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            // 4. Persist the structural CHILD_OF edge when a parent is present (§7).
            if (command.ParentNodeId is { } parentId)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_DecisionNodeEdge
                        (NodeEdgeId, MatterId, FromNodeId, ToNodeId, EdgeKindCode, EdgeTypeCode, EdgeVersion, TenantId, CreatedByUserId)
                    VALUES
                        (NEWID(), @MatterId, @FromNodeId, @ToNodeId, N'Structural', N'CHILD_OF', 1, @TenantId, @Actor);
                    """,
                    new { command.MatterId, FromNodeId = nodeId, ToNodeId = parentId, TenantId = tenantId, Actor = actorUserId },
                    transaction: tx, cancellationToken: cancellationToken));
            }

            var result = new CommitResult(
                nodeId, assessmentId, approvalId, 1,
                approvalId is null ? (command.RequestApproval ? "Submitted" : "Current") : "ApprovedForMatter",
                changeEventId, ReevaluationQueued: true);

            // 5. Immutable audit record (idempotency key stored as CausationId, §16/§23/§25).
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionNodeAudit
                    (NodeAuditId, MatterId, DecisionNodeId, ActionCode, DetailJson, CorrelationId, CausationId, BaseSnapshotId, TenantId, CreatedByUserId)
                VALUES
                    (NEWID(), @MatterId, @DecisionNodeId, N'NodeAdded', @DetailJson, @ChangeEventId, @Key, @BaseSnapshotId, @TenantId, @Actor);
                """,
                new
                {
                    command.MatterId, DecisionNodeId = nodeId, DetailJson = JsonSerializer.Serialize(result),
                    ChangeEventId = changeEventId, Key = command.IdempotencyKey, command.BaseSnapshotId,
                    TenantId = tenantId, Actor = actorUserId
                },
                transaction: tx, cancellationToken: cancellationToken));

            // 6. Transactional outbox event — CDC transports the committed change (§17). No LLM/IO in tx.
            await EnqueueOutboxAsync(connection, tx, "DecisionNodeRegistered", JsonSerializer.Serialize(new
            {
                tenantId, command.MatterId, decisionNodeId = nodeId, assessmentId, approvalId,
                changeEventId, correlationId = changeEventId, causationId = command.IdempotencyKey,
                baseSnapshotId = command.BaseSnapshotId, command.CandidateNodeId,
                origin = "AttorneySupplied", actorUserId
            }), cancellationToken);

            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<AttorneyRelativeAssessmentDto> SubmitAssessmentAsync(
        Guid tenantId, Guid actorUserId, SubmitAttorneyAssessmentCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var assessmentId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AttorneyRelativeAssessment
                    (AssessmentId, MatterId, DecisionNodeId, AttorneyUserId, ConfirmedValue, SuggestedMidpoint,
                     PreviousSiblingId, PreviousSiblingValue, NextSiblingId, NextSiblingValue, DecisionSnapshotId,
                     MethodCode, Rationale, StatusCode, AssessmentVersion, TenantId, CreatedByUserId)
                VALUES
                    (@AssessmentId, @MatterId, @DecisionNodeId, @Actor, @ConfirmedValue, @SuggestedMidpoint,
                     @PreviousSiblingId, @PreviousSiblingValue, @NextSiblingId, @NextSiblingValue, @BaseSnapshotId,
                     @MethodCode, @Rationale, N'Submitted', 1, @TenantId, @Actor);
                """,
                new
                {
                    AssessmentId = assessmentId, command.MatterId, command.DecisionNodeId, Actor = actorUserId,
                    command.ConfirmedValue, command.SuggestedMidpoint, command.PreviousSiblingId, command.PreviousSiblingValue,
                    command.NextSiblingId, command.NextSiblingValue, command.BaseSnapshotId, command.MethodCode,
                    command.Rationale, TenantId = tenantId
                },
                transaction: tx, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionNodeAudit
                    (NodeAuditId, MatterId, DecisionNodeId, ActionCode, CausationId, BaseSnapshotId, TenantId, CreatedByUserId)
                VALUES (NEWID(), @MatterId, @DecisionNodeId, N'AssessmentSubmitted', @Key, @BaseSnapshotId, @TenantId, @Actor);
                """,
                new { command.MatterId, command.DecisionNodeId, Key = command.IdempotencyKey, command.BaseSnapshotId, TenantId = tenantId, Actor = actorUserId },
                transaction: tx, cancellationToken: cancellationToken));

            await EnqueueOutboxAsync(connection, tx, "AttorneyAssessmentSubmitted", JsonSerializer.Serialize(new
            {
                tenantId, command.MatterId, command.DecisionNodeId, assessmentId, actorUserId, correlationId = assessmentId
            }), cancellationToken);

            tx.Commit();
            return new AttorneyRelativeAssessmentDto(
                assessmentId, command.DecisionNodeId, actorUserId, string.Empty, null, command.ConfirmedValue,
                command.SuggestedMidpoint, command.PreviousSiblingId, command.PreviousSiblingValue, command.NextSiblingId,
                command.NextSiblingValue, command.MethodCode, command.Rationale, "Submitted", 1, DateTime.UtcNow);
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<ApprovedMatterAssessmentDto> ApproveAssessmentAsync(
        Guid tenantId, Guid actorUserId, ApproveMatterAssessmentCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var approvalId = Guid.NewGuid();
            // Enforce single active approved assessment per node (§5): deactivate prior, insert new, flag statuses.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_ApprovedMatterAssessment
                SET IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionNodeId = @DecisionNodeId AND IsActive = 1 AND IsDeleted = 0;

                UPDATE POLOXI.Legal_AttorneyRelativeAssessment
                SET StatusCode = N'Superseded', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionNodeId = @DecisionNodeId
                  AND StatusCode = N'ApprovedForMatter' AND IsDeleted = 0;

                INSERT INTO POLOXI.Legal_ApprovedMatterAssessment
                    (ApprovalId, MatterId, DecisionNodeId, AssessmentId, ApprovedByUserId, GovernancePolicyCode,
                     DecisionSnapshotId, ApprovalVersion, IsActive, TenantId, CreatedByUserId)
                VALUES
                    (@ApprovalId, @MatterId, @DecisionNodeId, @AssessmentId, @Actor, @GovernancePolicyCode,
                     @BaseSnapshotId, 1, 1, @TenantId, @Actor);

                UPDATE POLOXI.Legal_AttorneyRelativeAssessment
                SET StatusCode = N'ApprovedForMatter', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                WHERE AssessmentId = @AssessmentId;
                """,
                new
                {
                    ApprovalId = approvalId, command.MatterId, command.DecisionNodeId, command.AssessmentId,
                    Actor = actorUserId, command.GovernancePolicyCode, command.BaseSnapshotId, TenantId = tenantId
                },
                transaction: tx, cancellationToken: cancellationToken));

            var confirmedValue = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "SELECT ConfirmedValue FROM POLOXI.Legal_AttorneyRelativeAssessment WHERE AssessmentId = @AssessmentId;",
                new { command.AssessmentId }, transaction: tx, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionNodeAudit
                    (NodeAuditId, MatterId, DecisionNodeId, ActionCode, CausationId, BaseSnapshotId, TenantId, CreatedByUserId)
                VALUES (NEWID(), @MatterId, @DecisionNodeId, N'AssessmentApproved', @Key, @BaseSnapshotId, @TenantId, @Actor);
                """,
                new { command.MatterId, command.DecisionNodeId, Key = command.IdempotencyKey, command.BaseSnapshotId, TenantId = tenantId, Actor = actorUserId },
                transaction: tx, cancellationToken: cancellationToken));

            await EnqueueOutboxAsync(connection, tx, "MatterAssessmentApproved", JsonSerializer.Serialize(new
            {
                tenantId, command.MatterId, command.DecisionNodeId, command.AssessmentId, approvalId,
                command.GovernancePolicyCode, actorUserId, correlationId = approvalId
            }), cancellationToken);

            tx.Commit();
            return new ApprovedMatterAssessmentDto(
                approvalId, command.DecisionNodeId, command.AssessmentId, confirmedValue, actorUserId,
                string.Empty, command.GovernancePolicyCode, DateTime.UtcNow);
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<Guid> RaiseChallengeAsync(
        Guid tenantId, Guid actorUserId, RaiseChallengeCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            var challengeId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AttorneyDecisionChallenge
                    (ChallengeId, MatterId, DecisionNodeId, AttorneyUserId, ChallengeTypeCode, ChallengeText, StatusCode, TenantId, CreatedByUserId)
                VALUES
                    (@ChallengeId, @MatterId, @DecisionNodeId, @Actor, @ChallengeTypeCode, @ChallengeText, N'Open', @TenantId, @Actor);
                """,
                new
                {
                    ChallengeId = challengeId, command.MatterId, command.DecisionNodeId, Actor = actorUserId,
                    command.ChallengeTypeCode, command.ChallengeText, TenantId = tenantId
                },
                transaction: tx, cancellationToken: cancellationToken));

            await EnqueueOutboxAsync(connection, tx, "AttorneyDecisionInputSubmitted", JsonSerializer.Serialize(new
            {
                tenantId, command.MatterId, command.DecisionNodeId, challengeId, kind = "Challenge",
                command.ChallengeTypeCode, actorUserId, correlationId = challengeId
            }), cancellationToken);

            tx.Commit();
            return challengeId;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<CommitResult> RepositionAsync(
        Guid tenantId, Guid actorUserId, RepositionNodeCommand command, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Optimistic concurrency: only reposition when the caller's expected node version still holds (§23).
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_DecisionNode
                SET NodeVersion = NodeVersion + 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionNodeId = @DecisionNodeId
                  AND NodeVersion = @ExpectedNodeVersion AND IsDeleted = 0;
                """,
                new { TenantId = tenantId, command.MatterId, command.DecisionNodeId, command.ExpectedNodeVersion, Actor = actorUserId },
                transaction: tx, cancellationToken: cancellationToken));
            if (affected == 0)
                throw new InvalidOperationException("The decision model changed since this reposition was prepared. Re-preview before submitting.");

            // Repositioning creates a NEW assessment version rather than rewriting history (§4).
            var assessmentId = Guid.NewGuid();
            var newVersion = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT NodeVersion FROM POLOXI.Legal_DecisionNode WHERE DecisionNodeId = @DecisionNodeId;",
                new { command.DecisionNodeId }, transaction: tx, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_AttorneyRelativeAssessment
                SET StatusCode = N'Superseded', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionNodeId = @DecisionNodeId
                  AND AttorneyUserId = @Actor AND StatusCode IN (N'Current', N'Submitted') AND IsDeleted = 0;

                INSERT INTO POLOXI.Legal_AttorneyRelativeAssessment
                    (AssessmentId, MatterId, DecisionNodeId, AttorneyUserId, ConfirmedValue, SuggestedMidpoint,
                     PreviousSiblingId, PreviousSiblingValue, NextSiblingId, NextSiblingValue, DecisionSnapshotId,
                     MethodCode, Rationale, StatusCode, AssessmentVersion, TenantId, CreatedByUserId)
                VALUES
                    (@AssessmentId, @MatterId, @DecisionNodeId, @Actor, @ConfirmedValue, NULL,
                     @PreviousSiblingId, @PreviousSiblingValue, @NextSiblingId, @NextSiblingValue, @BaseSnapshotId,
                     @MethodCode, @Rationale, N'Current', @NewVersion, @TenantId, @Actor);
                """,
                new
                {
                    AssessmentId = assessmentId, command.MatterId, command.DecisionNodeId, Actor = actorUserId,
                    command.ConfirmedValue, command.PreviousSiblingId, command.PreviousSiblingValue, command.NextSiblingId,
                    command.NextSiblingValue, command.BaseSnapshotId, command.MethodCode, command.Rationale,
                    NewVersion = newVersion, TenantId = tenantId
                },
                transaction: tx, cancellationToken: cancellationToken));

            var changeEventId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionNodeAudit
                    (NodeAuditId, MatterId, DecisionNodeId, ActionCode, CorrelationId, CausationId, BaseSnapshotId, TenantId, CreatedByUserId)
                VALUES (NEWID(), @MatterId, @DecisionNodeId, N'NodeRepositioned', @ChangeEventId, @Key, @BaseSnapshotId, @TenantId, @Actor);
                """,
                new { command.MatterId, command.DecisionNodeId, ChangeEventId = changeEventId, Key = command.IdempotencyKey, command.BaseSnapshotId, TenantId = tenantId, Actor = actorUserId },
                transaction: tx, cancellationToken: cancellationToken));

            await EnqueueOutboxAsync(connection, tx, "DecisionReevaluationRequested", JsonSerializer.Serialize(new
            {
                tenantId, command.MatterId, command.DecisionNodeId, assessmentId, changeEventId,
                reason = "PropositionRepositioned", actorUserId, correlationId = changeEventId
            }), cancellationToken);

            tx.Commit();
            return new CommitResult(command.DecisionNodeId, assessmentId, null, newVersion, "Current", changeEventId, true);
        }
        catch { tx.Rollback(); throw; }
    }

    // ── §2/§7 resolve-or-create the decision node for a selected Wide branch (and its ancestor chain) ──
    // The chain is ordered root (L1) → selected branch. Each branch is materialized once per matter,
    // keyed on SourceWideBranchId, so repeated "Add proposition from this branch" actions reuse nodes.
    public async Task<ResolvedBranchNode> ResolveBranchNodeAsync(
        Guid tenantId, Guid actorUserId, ResolveBranchNodeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AncestorChain is null || command.AncestorChain.Count == 0)
            throw new InvalidOperationException("ResolveBranchNodeAsync requires a non-empty ancestor chain.");

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            Guid? parentNodeId = null;
            Guid candidateNodeId = Guid.Empty;
            Guid resolvedNodeId = Guid.Empty;
            string resolvedCanonicalKey = string.Empty;
            int resolvedLevel = 0;
            bool resolvedCreated = false;

            foreach (var branch in command.AncestorChain)
            {
                var canonicalKey = $"wb:{branch.WideBranchId:N}";

                // Resolve existing materialized node for this Wide branch (idempotent).
                var existing = await connection.QuerySingleOrDefaultAsync<(Guid DecisionNodeId, Guid? ParentNodeId, string CanonicalKey, int NodeLevel)?>(
                    new CommandDefinition(
                        """
                        SELECT TOP 1 DecisionNodeId, ParentNodeId, CanonicalKey, NodeLevel
                        FROM POLOXI.Legal_DecisionNode
                        WHERE TenantId = @TenantId AND MatterId = @MatterId
                          AND SourceWideBranchId = @WideBranchId AND IsDeleted = 0;
                        """,
                        new { TenantId = tenantId, command.MatterId, branch.WideBranchId },
                        transaction: tx, cancellationToken: cancellationToken));

                Guid nodeId;
                bool created;
                if (existing is { } row)
                {
                    nodeId = row.DecisionNodeId;
                    resolvedCanonicalKey = row.CanonicalKey;
                    created = false;
                }
                else
                {
                    nodeId = Guid.NewGuid();
                    resolvedCanonicalKey = canonicalKey;
                    created = true;
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO POLOXI.Legal_DecisionNode
                            (DecisionNodeId, MatterId, ParentNodeId, CanonicalKey, NodeKindCode, NodeLevel, NodeText,
                             OriginCode, NodeVersion, StructuralStateCode, EvidenceStateCode, AuthorityStateCode,
                             SourceWideBranchId, TenantId, CreatedByUserId)
                        VALUES
                            (@DecisionNodeId, @MatterId, @ParentNodeId, @CanonicalKey, @NodeKindCode, @NodeLevel, @NodeText,
                             N'SystemDerived', 1, N'Valid', N'NotEvaluated', N'NotRequired',
                             @WideBranchId, @TenantId, @Actor);
                        """,
                        new
                        {
                            DecisionNodeId = nodeId, command.MatterId, ParentNodeId = parentNodeId, CanonicalKey = canonicalKey,
                            branch.NodeKindCode, branch.NodeLevel, branch.NodeText, branch.WideBranchId,
                            TenantId = tenantId, Actor = actorUserId
                        },
                        transaction: tx, cancellationToken: cancellationToken));

                    if (parentNodeId is { } parentId)
                    {
                        await connection.ExecuteAsync(new CommandDefinition(
                            """
                            IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionNodeEdge
                                           WHERE TenantId = @TenantId AND MatterId = @MatterId
                                             AND FromNodeId = @FromNodeId AND ToNodeId = @ToNodeId AND EdgeTypeCode = N'CHILD_OF')
                            INSERT INTO POLOXI.Legal_DecisionNodeEdge
                                (NodeEdgeId, MatterId, FromNodeId, ToNodeId, EdgeKindCode, EdgeTypeCode, EdgeVersion, TenantId, CreatedByUserId)
                            VALUES
                                (NEWID(), @MatterId, @FromNodeId, @ToNodeId, N'Structural', N'CHILD_OF', 1, @TenantId, @Actor);
                            """,
                            new { command.MatterId, FromNodeId = nodeId, ToNodeId = parentId, TenantId = tenantId, Actor = actorUserId },
                            transaction: tx, cancellationToken: cancellationToken));
                    }
                }

                if (candidateNodeId == Guid.Empty) candidateNodeId = nodeId;
                resolvedNodeId = nodeId;
                resolvedLevel = branch.NodeLevel;
                resolvedCreated = created;
                parentNodeId = nodeId; // next descendant's parent is this node
            }

            // parentNodeId now equals the selected node; the actual parent is the one before it.
            Guid? selectedParent = command.AncestorChain.Count >= 2
                ? await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                    """
                    SELECT ParentNodeId FROM POLOXI.Legal_DecisionNode
                    WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionNodeId = @DecisionNodeId;
                    """,
                    new { TenantId = tenantId, command.MatterId, DecisionNodeId = resolvedNodeId },
                    transaction: tx, cancellationToken: cancellationToken))
                : null;

            tx.Commit();
            return new ResolvedBranchNode(resolvedNodeId, candidateNodeId, selectedParent, resolvedLevel, resolvedCanonicalKey, resolvedCreated);
        }
        catch { tx.Rollback(); throw; }
    }

    // Transactional-outbox enqueue that enlists in the caller's transaction (reuses SaaS.SaaS_Outbox, §17).
    private static async Task EnqueueOutboxAsync(IDbConnection connection, IDbTransaction tx, string messageType, string payloadJson, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT SaaS.SaaS_Outbox (OutboxId, MessageType, PayloadJson, StatusCode)
            VALUES (NEWID(), @MessageType, @PayloadJson, N'Pending');
            """,
            new { MessageType = messageType, PayloadJson = payloadJson },
            transaction: tx, cancellationToken: cancellationToken));
    }

    private sealed record SiblingRow(Guid DecisionNodeId, string NodeText, decimal? Value);
    private sealed record DuplicateRow(Guid DecisionNodeId, string CanonicalKey, string NodeText);

    private sealed record NodeRow(
        Guid DecisionNodeId,
        Guid? ParentNodeId,
        string CanonicalKey,
        string NodeKindCode,
        int NodeLevel,
        string NodeText,
        string OriginCode,
        string? PlacementKey,
        string StructuralStateCode,
        string EvidenceStateCode,
        string AuthorityStateCode,
        decimal? EvaluatedValue,
        long NodeVersion,
        int OpenChallengeCount);
}
